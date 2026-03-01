using System.Net.Sockets;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CDUGlue
{
    public class TextBlock
    {
        public string Text { get; set; } = string.Empty;
        public string? Color { get; set; }
        public string? Size { get; set; }
    }

    public class Line
    {
        public List<TextBlock> Upper { get; set; } = new();
        public List<TextBlock> Lower { get; set; } = new();
    }

    public class ScreenUpdate
    {
        public List<TextBlock> Title { get; set; } = new();
        public string PageNumber { get; set; } = string.Empty;
        public Line[] Lines { get; set; } = new Line[6];

        public ScreenUpdate()
        {
            for (int i = 0; i < 6; i++)
            {
                Lines[i] = new Line();
            }
        }
    }

    public class CduClient
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private readonly string _ip;
        private readonly int _port;
        private CancellationTokenSource? _cts;
        private string _lastRawXml = string.Empty;

        public event Action<bool>? PowerChanged;
        public event Action<bool>? MessageChanged;
        public event Action<bool>? ExecChanged;
        public event Action<bool>? FailChanged;
        public event Action<string>? ScratchpadChanged;
        public event Action<ScreenUpdate>? ScreenChanged;

        public string LastRawXml => _lastRawXml;

        public CduClient(string ip, int port)
        {
            _ip = ip;
            _port = port;
        }

        public async Task ConnectAsync()
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_ip, _port);
            _stream = _client.GetStream();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoop(_cts.Token));
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[1024 * 64];
            var sb = new StringBuilder();

            try
            {
                while (!token.IsCancellationRequested && _client?.Connected == true)
                {
                    int bytesRead = await _stream!.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0) break;

                    string fragment = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    sb.Append(fragment);
                    _lastRawXml = fragment; // Store last received fragment

                    string currentText = sb.ToString();

                    // The XML stream can contain multiple root-level elements like <power />, <screen>...</screen>, etc.
                    // We need to parse them as fragments.
                    
                    try 
                    {
                        // Wrap in a dummy root to parse multiple fragments if they are complete
                        string wrapped = $"<root>{currentText}</root>";
                        var doc = XDocument.Parse(wrapped);
                        foreach (var element in doc.Root!.Elements())
                        {
                            ParseElement(element);
                        }
                        sb.Clear();
                    }
                    catch (XmlException)
                    {
                        // Incomplete XML, wait for more data. 
                        // This is a bit naive but should work for this stream if elements are not huge.
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ReceiveLoop error: {ex.Message}");
            }
        }

        private void ParseElement(XElement element)
        {
            switch (element.Name.LocalName)
            {
                case "power":
                    PowerChanged?.Invoke(GetState(element));
                    break;
                case "fail":
                    FailChanged?.Invoke(GetState(element));
                    break;
                case "message":
                    MessageChanged?.Invoke(GetState(element));
                    break;
                case "exec":
                    ExecChanged?.Invoke(GetState(element));
                    break;
                case "scratchpad":
                    ScratchpadChanged?.Invoke(MapSpecialChars(element.Value));
                    break;
                case "screen":
                    ParseScreen(element);
                    break;
            }
        }

        private bool GetState(XElement element)
        {
            return element.Attribute("state")?.Value.ToLower() == "true";
        }

        private void ParseScreen(XElement screenElement)
        {
            var update = new ScreenUpdate();
            update.Title = ParseTextBlocks(screenElement.Element("title"));
            update.PageNumber = screenElement.Element("pageNumber")?.Value ?? string.Empty;

            for (int i = 1; i <= 6; i++)
            {
                var lineElement = screenElement.Element($"line{i}");
                if (lineElement != null)
                {
                    update.Lines[i - 1].Upper = ParseTextBlocks(lineElement.Element("upper"));
                    update.Lines[i - 1].Lower = ParseTextBlocks(lineElement.Element("lower"));
                }
            }
            ScreenChanged?.Invoke(update);
        }

        private List<TextBlock> ParseTextBlocks(XElement? element)
        {
            var blocks = new List<TextBlock>();
            if (element == null) return blocks;

            foreach (var node in element.Nodes())
            {
                if (node is XElement child)
                {
                    blocks.AddRange(ParseRecursive(child, null, null));
                }
                else if (node is XText text)
                {
                    blocks.Add(new TextBlock { Text = MapSpecialChars(text.Value), Color = null, Size = null });
                }
            }
            return blocks;
        }

        private List<TextBlock> ParseRecursive(XElement element, string? color, string? size)
        {
            var result = new List<TextBlock>();
            string? currentColor = color;
            string? currentSize = size;

            // Map tags to color/size
            switch (element.Name.LocalName)
            {
                case "small": currentSize = "small"; break;
                case "large": currentSize = "large"; break;
                case "cyan": currentColor = "cyan"; break;
                case "green": currentColor = "green"; break;
                case "magenta": currentColor = "magenta"; break;
                case "text":
                    var colorAttr = element.Attribute("color")?.Value;
                    if (colorAttr != null) currentColor = colorAttr;
                    var sizeAttr = element.Attribute("size")?.Value;
                    if (sizeAttr != null) currentSize = sizeAttr;
                    break;
            }

            if (!element.HasElements)
            {
                result.Add(new TextBlock 
                { 
                    Text = MapSpecialChars(element.Value), 
                    Color = currentColor, 
                    Size = currentSize 
                });
            }
            else
            {
                foreach (var node in element.Nodes())
                {
                    if (node is XElement child)
                    {
                        result.AddRange(ParseRecursive(child, currentColor, currentSize));
                    }
                    else if (node is XText text)
                    {
                        result.Add(new TextBlock 
                        { 
                            Text = MapSpecialChars(text.Value), 
                            Color = currentColor, 
                            Size = currentSize 
                        });
                    }
                }
            }

            return result;
        }

        private string MapSpecialChars(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input.Replace("#", "☐").Replace("'", "°");
        }

        public async Task SendKey(string key)
        {
            if (_stream != null && _client?.Connected == true)
            {
                byte[] data = Encoding.UTF8.GetBytes(key + "\n");
                await _stream.WriteAsync(data, 0, data.Length);
            }
        }
    }
}

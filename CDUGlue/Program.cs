using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McduDotNet;
using CDUGlue;

namespace CDUGlue
{
    public class Program
    {
        private static ICdu? cdu;
        private static CduClient? cduClient;

        public static async Task Main(string[] args)
        {
            if (args.Length != 3 && args.Length != 0)
            {
                PrintUsage();
                return;
            }

            if (args.Length == 0)
            {
                PrintUsage();
                PrintLocalDevices();
                return;
            }

            string prosimCDUIpAddress = args[0];
            if (!int.TryParse(args[1], out int prosimCDUPort))
            {
                Console.WriteLine("Invalid prosimCDUPort. Must be an integer.");
                return;
            }

            int winwingCDUProductId;
            try
            {
                winwingCDUProductId = Convert.ToInt32(args[2], 16);
            }
            catch
            {
                Console.WriteLine("Invalid winwingCDUProductId. Must be a hex integer.");
                return;
            }

            var devices = CduFactory.FindLocalDevices();
            var matchedDevice = devices.FirstOrDefault(d => d.UsbProductId == winwingCDUProductId);

            if (matchedDevice == null)
            {
                Console.WriteLine($"Error: No device matches USB Product ID 0x{winwingCDUProductId:X4}.");
                PrintLocalDevices();
                return;
            }

            cdu = CduFactory.ConnectLocal(matchedDevice);
            if (cdu == null)
            {
                Console.WriteLine("Error: Failed to connect to winwing CDU device.");
                return;
            }

            cduClient = new CduClient(prosimCDUIpAddress, prosimCDUPort);
            
            Console.WriteLine($"Connecting to ProsimCDU at {prosimCDUIpAddress}:{prosimCDUPort}...");
            await cduClient.ConnectAsync();
            Console.WriteLine("Connected to ProsimCDU.");

            // Subscribe to LED events
            cduClient.MessageChanged += (on) => { cdu.Leds.SetLed(Led.Msg, on); cdu.RefreshLeds(); };
            cduClient.ExecChanged += (on) => { cdu.Leds.SetLed(Led.Exec, on); cdu.RefreshLeds(); };
            cduClient.FailChanged += (on) => { cdu.Leds.SetLed(Led.Fail, on); cdu.RefreshLeds(); };

            // Subscribe to scratchpad events
            cduClient.ScratchpadChanged += (text) =>
            {
                cdu.Output.BottomLine().ClearRow().Write(text);
                cdu.RefreshDisplay();
            };

            // Subscribe to CDU key events
            cdu.KeyDown += (sender, e) =>
            {
                string? keyStr = getKeyToSend(e.Key);
                string valueToSend = keyStr ?? e.Character;
                _ = cduClient.SendKey(valueToSend + "\n");
            };

            // Subscribe to screen change events
            cduClient.ScreenChanged += (update) =>
            {
                var compositor = cdu.Output;
                compositor.TopLine().StartOfLine();

                // a) title by calling displayTextBlocks() with centered=true
                displayTextBlocks(update.Title, compositor, true);

                // b) "page number" in white color by calling .Color() at the end of first row
                compositor.Color(Colour.White);
                compositor.RightToLeft();
                compositor.Write(update.PageNumber);
                compositor.LeftToRight();

                // c) Complete first line by moving down via compositor's NewLine()
                compositor.NewLine();

                // d) Loop over Lines and display their Upper then Lower parts by calling displayTextBlocks() and NewLine()
                foreach (var line in update.Lines)
                {
                    displayTextBlocks(line.Upper, compositor);
                    compositor.NewLine();
                    displayTextBlocks(line.Lower, compositor);
                    compositor.NewLine();
                }

                cdu.RefreshDisplay();
            };

            // Keep the application running
            await Task.Delay(-1);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: CDUGlue <prosimCDUIpAddress> <prosimCDUPort> <winwingCDUProductId (hex)>");
        }

        private static void PrintLocalDevices()
        {
            Console.WriteLine("Local winwing CDU devices:");
            var devices = CduFactory.FindLocalDevices();
            foreach (var device in devices)
            {
                Console.WriteLine($"- Name: {device.Device}, USB Product ID: 0x{device.UsbProductId:X4}, Vendor ID: 0x{device.UsbVendorId:X4}");
            }
        }

        private static string? getKeyToSend(Key key)
        {
            return key switch
            {
                Key.LineSelectLeft1 => "LSKL1",
                Key.LineSelectLeft2 => "LSKL2",
                Key.LineSelectLeft3 => "LSKL3",
                Key.LineSelectLeft4 => "LSKL4",
                Key.LineSelectLeft5 => "LSKL5",
                Key.LineSelectLeft6 => "LSKL6",
                Key.LineSelectRight1 => "LSKR1",
                Key.LineSelectRight2 => "LSKR2",
                Key.LineSelectRight3 => "LSKR3",
                Key.LineSelectRight4 => "LSKR4",
                Key.LineSelectRight5 => "LSKR5",
                Key.LineSelectRight6 => "LSKR6",
                Key.Exec => "EXEC",
                Key.Clb => "CLB",
                Key.Clr => "CLEAR",
                Key.Crz => "CRZ",
                Key.Del => "DEL",
                Key.DepArr => "DEP_ARR",
                Key.Des => "DES",
                Key.Fix => "FIX",
                Key.Hold => "HOLD",
                Key.InitRef => "INIT_REF",
                Key.Legs => "LEGS",
                Key.Menu => "MENU",
                Key.N1Limit => "N1",
                Key.NextPage => "NEXT",
                Key.PrevPage => "PREV",
                Key.Prog => "PROG",
                Key.Rte => "RTE",
                _ => null
            };
        }

        private static void displayTextBlocks(List<TextBlock> blocks, Compositor compositor, bool centered = false)
        {
            // e) Calls ClearRow() of compositor to ensure row/line is clear as a first operation.
            compositor.ClearRow();

            foreach (var block in blocks)
            {
                // c) For each block set compositor's color (compositor.Color method) to a color 
                // and size (compositors Large/Small methods) to match that of text block values.
                // If text block does not have color or it does not match, use default color of White.
                
                if (!Enum.TryParse<Colour>(block.Color, true, out var color))
                {
                    color = Colour.White;
                }
                compositor.Color(color);

                if (string.Equals(block.Size, "small", StringComparison.OrdinalIgnoreCase))
                {
                    compositor.Small();
                }
                else
                {
                    compositor.Large();
                }

                // d) If centered is true, call compositor's .Centered method with text block's text;
                // if centered is false call .Write with same.
                if (centered)
                {
                    compositor.Centered(block.Text);
                }
                else
                {
                    compositor.Write(block.Text);
                }
            }
            // f) Does not advance to the new line by calling any such methods on the compositor.
        }
    }
}

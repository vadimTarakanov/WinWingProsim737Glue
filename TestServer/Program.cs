using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestServer;

public class Program
{
    private static readonly string[] Updates = new[]
    {
        @"<power state=""true"" />",
        @"<message state=""false"" />",
        @"<exec state=""false"" />",
        @"<fail state=""true"" />",
        @"<scratchpad><![CDATA[]]></scratchpad>",
        @"<screen>
  <title>
    <text size=""large""><![CDATA[SIMULATOR CONTROL   1/ 2]]></text>
  </title>
  <pageNumber><![CDATA[]]></pageNumber>
  <line1>
    <upper>
      <text size=""small""><![CDATA[                        ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[*PAUSE]]><![CDATA[    ]]><![CDATA[FLIGHT FREEZE*]]></text>
    </lower>
  </line1>
  <line2>
    <upper>
      <text size=""small""><![CDATA[                ]]><![CDATA[SIM RATE]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[<FUEL]]><![CDATA[      ]]><text size=""small""><![CDATA[NORMAL]]></text><![CDATA[/]]><text size=""small""><![CDATA[2X]]></text><![CDATA[/]]><text size=""small""><![CDATA[4X]]></text><![CDATA[>]]></text>
    </lower>
  </line2>
  <line3>
    <upper>
      <text size=""small""><![CDATA[              ]]><![CDATA[FAULTS AND]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[<PAX/CARGO]]><![CDATA[  ]]><![CDATA[MAINTENANCE>]]></text>
    </lower>
  </line3>
  <line4>
    <upper>
      <text size=""small""><![CDATA[                        ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[<DOORS]]><![CDATA[           ]]><![CDATA[RADIOS>]]></text>
    </lower>
  </line4>
  <line5>
    <upper>
      <text size=""small""><![CDATA[                        ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[<GROUND SERVICE]]><![CDATA[         ]]></text>
    </lower>
  </line5>
  <line6>
    <upper>
      <text size=""small""><![CDATA[                        ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[<RESET FMS]]><![CDATA[              ]]></text>
    </lower>
  </line6>
</screen>",
        @"<fail state=""true"" />",
        @"<screen>
  <title>
    <text size=""large"">
      <text color=""cyan""><![CDATA[RTE LEGS]]></text>
    </text>
  </title>
  <pageNumber><![CDATA[1/1]]></pageNumber>
  <line1>
    <upper>
      <text size=""small""><![CDATA[                        ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[                        ]]></text>
    </lower>
  </line1>
  <line2>
    <upper>
      <text size=""small""><![CDATA[                        ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[                        ]]></text>
    </lower>
  </line2>
  <line3>
    <upper>
      <text size=""small""><![CDATA[                        ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[                        ]]></text>
    </lower>
  </line3>
  <line4>
    <upper>
      <text size=""small""><![CDATA[                        ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[                        ]]></text>
    </lower>
  </line4>
  <line5>
    <upper>
      <text size=""small""><![CDATA[                        ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[                        ]]></text>
    </lower>
  </line5>
  <line6>
    <upper>
      <text size=""small""><![CDATA[--------HOLD AT---------]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[#####]]><![CDATA[              ]]><![CDATA[PPOS>]]></text>
    </lower>
  </line6>
</screen>",
        @"<screen>
  <title>
    <text size=""large""><![CDATA[     GROUND SERVICE     ]]></text>
  </title>
  <pageNumber><![CDATA[]]></pageNumber>
  <line1>
    <upper>
      <text size=""small""><![CDATA[                        ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[                        ]]></text>
    </lower>
  </line1>
  <line2>
    <upper>
      <text size=""small""><![CDATA[GND PWR]]><![CDATA[          ]]><![CDATA[GND AIR]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[<]]><text size=""small""><![CDATA[ON]]></text><![CDATA[/]]><text color=""green""><![CDATA[OFF]]></text><![CDATA[          ]]><![CDATA[<]]><text size=""small""><![CDATA[ON]]></text><![CDATA[/]]><text color=""green""><![CDATA[OFF]]></text></text>
    </lower>
  </line2>
  <line3>
    <upper>
      <text size=""small""><![CDATA[TOW PIN]]><![CDATA[                 ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[<]]><text color=""green""><![CDATA[ON]]></text><![CDATA[/]]><text size=""small""><![CDATA[OFF]]></text><![CDATA[                 ]]></text>
    </lower>
  </line3>
  <line4>
    <upper>
      <text size=""small""><![CDATA[       ]]><![CDATA[PUSHBACK]]><![CDATA[         ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[<]]><text color=""green""><![CDATA[C]]></text><![CDATA[/]]><text size=""small""><![CDATA[L]]></text><![CDATA[/]]><text size=""small""><![CDATA[R]]></text><![CDATA[             ]]><![CDATA[STOP>]]></text>
    </lower>
  </line4>
  <line5>
    <upper>
      <text size=""small""><![CDATA[                        ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[              ]]><![CDATA[REFUELING>]]></text>
    </lower>
  </line5>
  <line6>
    <upper>
      <text size=""small""><![CDATA[                        ]]></text>
    </upper>
    <lower>
      <text size=""large""><![CDATA[<RETURN]]><![CDATA[                 ]]></text>
    </lower>
  </line6>
</screen>"
    };

    public static async Task Main(string[] args)
    {
        var listener = new TcpListener(IPAddress.Any, 1235);
        listener.Start();
        Console.WriteLine("Test server started on port 1235...");

        while (true)
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("Client connected.");
                using var stream = client.GetStream();

                while (client.Connected)
                {
                    foreach (var update in Updates)
                    {
                        byte[] data = Encoding.UTF8.GetBytes(update);
                        await stream.WriteAsync(data, 0, data.Length);
                        Console.WriteLine($"Sent: {update.Substring(0, Math.Min(update.Length, 50))}...");
                    }
                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}

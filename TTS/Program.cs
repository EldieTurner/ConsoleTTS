using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TTS;

class Program
{
    static async Task Main(string[] args)
    {
        // Check for help flag.
        if (args.Length > 0 && (args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                                 args[0].Equals("-h", StringComparison.OrdinalIgnoreCase)))
        {
            PrintUsage();
            return;
        }
        // Build configuration from appsettings.json and environment variables.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Retrieve configuration settings.
        string apiUrl = configuration["TTSSettings:ApiUrl"] ?? throw new InvalidOperationException("ApiUrl is not configured.");
        string model = configuration["TTSSettings:Model"] ?? throw new InvalidOperationException("Model is not configured.");
        string responseFormat = configuration["TTSSettings:ResponseFormat"] ?? throw new InvalidOperationException("ResponseFormat is not configured.");

        Console.WriteLine($"Loaded API URL: {apiUrl}");
        Console.WriteLine($"Using model: {model}");
        Console.WriteLine($"Response format: {responseFormat}");

        // Retrieve OS-specific playback settings.
        string playerCommand = string.Empty;
        string playerArguments = string.Empty;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            playerCommand = configuration["Playback:Windows:PlayerCommand"] ?? throw new InvalidOperationException("PlayerCommand is not configured for Windows.");
            playerArguments = configuration["Playback:Windows:Arguments"] ?? throw new InvalidOperationException("Arguments are not configured for Windows.");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            playerCommand = configuration["Playback:Linux:PlayerCommand"] ?? throw new InvalidOperationException("PlayerCommand is not configured for Linux.");
            playerArguments = configuration["Playback:Linux:Arguments"] ?? throw new InvalidOperationException("Arguments are not configured for Linux.");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            playerCommand = configuration["Playback:OSX:PlayerCommand"] ?? throw new InvalidOperationException("PlayerCommand is not configured for OSX.");
            playerArguments = configuration["Playback:OSX:Arguments"] ?? throw new InvalidOperationException("Arguments are not configured for OSX.");
        }
        Console.WriteLine($"Player command for current OS: {playerCommand}");

        // Parse command-line arguments.
        // Supported options:
        //   --play              => play the audio returned from the API.
        //   --save              => save the audio to a file ("output.mp3").
        //   --playexisting <filepath> => play an already-saved MP3 file.
        bool playMode = false;
        bool saveMode = false;
        bool playExisting = false;
        string existingFilePath = "";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--playexisting", StringComparison.OrdinalIgnoreCase))
            {
                playExisting = true;
                if (i + 1 < args.Length)
                {
                    existingFilePath = args[i + 1];
                    i++; // Skip the file path argument.
                }
                else
                {
                    Console.WriteLine("Error: --playexisting requires a file path.");
                    return;
                }
            }
            else if (args[i].Equals("--play", StringComparison.OrdinalIgnoreCase))
            {
                playMode = true;
            }
            else if (args[i].Equals("--save", StringComparison.OrdinalIgnoreCase))
            {
                saveMode = true;
            }
        }

        // If --playexisting is specified, play that file and exit.
        if (playExisting)
        {
            if (!File.Exists(existingFilePath))
            {
                Console.WriteLine($"Error: file '{existingFilePath}' does not exist.");
                return;
            }
            await OpenAudioFileAsync(existingFilePath, playerCommand, playerArguments);
            return;
        }

        // Read piped text from standard input.
        string inputText = Console.In.ReadToEnd().Trim();
        if (string.IsNullOrWhiteSpace(inputText))
        {
            Console.WriteLine("No input text provided.");
            return;
        }

        // Create the JSON payload.
        var payload = new
        {
            input = inputText,
            model,
            response_format = responseFormat
        };

        string jsonPayload = JsonSerializer.Serialize(payload);

        using var client = new HttpClient();
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        // Call the TTS API.
        HttpResponseMessage response = await client.PostAsync(apiUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Error calling TTS API: " + response.StatusCode);
            return;
        }

        // Save the response to a temporary file.
        string tempFile = Path.Combine(Path.GetTempPath(), "speech.mp3");
        using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
        {
            await response.Content.CopyToAsync(fs);
        }
        Console.WriteLine($"Temporary audio file saved at: {tempFile}");

        // If save mode is enabled, copy the file to "output.mp3".
        if (saveMode)
        {
            string outputFile = "output.mp3";
            File.Copy(tempFile, outputFile, true);
            Console.WriteLine($"Audio also saved to: {outputFile}");
        }

        // If play mode is enabled, open the audio file using the OS-specific command.
        if (playMode)
        {
            await OpenAudioFileAsync(tempFile, playerCommand, playerArguments);
        }
    }

    // Method to print usage instructions for both Linux and Windows.
    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine();
        Console.WriteLine("  tts [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --play              Play the audio returned from the TTS API.");
        Console.WriteLine("  --save              Save the audio to a file (output.mp3).");
        Console.WriteLine("  --playexisting <filepath>");
        Console.WriteLine("                      Play an existing MP3 file from disk.");
        Console.WriteLine("  --help, -h          Display this help text.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine();
        Console.WriteLine("  Linux:");
        Console.WriteLine("    echo \"This is a test\" | tts --play --save");
        Console.WriteLine();
        Console.WriteLine("  Windows (Command Prompt):");
        Console.WriteLine("    type test.txt | tts --play");
        Console.WriteLine();
        Console.WriteLine("  Playing an existing file:");
        Console.WriteLine("    tts --playexisting \"C:\\path\\to\\file.mp3\"");
    }

    // Method to open the audio file using the configured player command and arguments.
    static async Task OpenAudioFileAsync(string filePath, string playerCommand, string playerArguments)
    {
        Console.WriteLine("Opening audio file...");
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // For Windows, if "default" is specified as the command, use cmd /c start.
                if (playerCommand.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{filePath}\"") { CreateNoWindow = true });
                }
                else
                {
                    Process.Start(playerCommand, $"{playerArguments} \"{filePath}\"");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start(playerCommand, $"{playerArguments} \"{filePath}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(playerCommand, $"{playerArguments} \"{filePath}\"");
            }
            else
            {
                Console.WriteLine("Default file opening is not supported on this operating system.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to open the file in the default player: " + ex.Message);
        }
        await Task.CompletedTask;
    }
}
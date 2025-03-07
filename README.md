# ConsoleTTS
This is a command‑line text-to-speech client that sends input text to a text-to-speech API (provided by a TTS container) and processes the resulting MP3 audio file. The tool supports playing the audio using a configurable, OS-specific media player and saving the file for later use.

## Features
- TTS API Integration:
- Sends text (via standard input) to a TTS API container and receives an MP3 file.
- Flexible Playback Options:
  - --play: Play the generated audio using the default or a configured media player.
  - --save: Save the generated audio as output.mp3.
  - --playexisting <filepath>: Play an already-saved MP3 file.
- OS-Specific Configuration:
- Playback settings (player command and arguments) are defined per operating system in a configuration file.
- Configuration via appsettings.json:
The application loads its configuration from an appsettings.json file (and environment variables), making it easy to adjust settings without modifying code.

## Prerequisites
- .NET 8.0 SDK
- A text-to-speech API container providing the TTS endpoint.
- A media player installed on your system:
  - Windows: Uses the default player (or a specified command).
  - Linux: Can be configured to use players like mpv (recommended for smooth playback).
  - macOS: Uses the open command by default.
## Configuration
The application uses an ``appsettings.json`` file to manage its configuration. This file includes TTS API settings and OS-specific playback settings.

Example ``appsettings.json``:

```json
{
  "TTSSettings": {
    "ApiUrl": "http://your-tts-api-endpoint/v1/audio/speech",
    "Model": "tts-1",
    "ResponseFormat": "mp3"
  },
  "Playback": {
    "Windows": {
      "PlayerCommand": "default",
      "Arguments": ""
    },
    "Linux": {
      "PlayerCommand": "mpv",
      "Arguments": "--force-window=yes"
    },
    "OSX": {
      "PlayerCommand": "open",
      "Arguments": ""
    }
  }
}
```
- TTSSettings:
Configure your API URL, TTS model, and response format.
- Playback:
Specify the command and any additional arguments for each OS.
On Windows, using `"default""` as the command will launch the default media player using `cmd /c start`.
## Running the Application
You can run the application directly with the .NET CLI.

### Examples
Generate Audio and Play It:

Linux/WSL:

```bash
echo "This is a test" | dotnet run -- --play
```
Windows (Command Prompt):

```cmd
type test.txt | dotnet run -- --play
```
- Generate Audio and Save It:

```bash
echo "This is a test" | dotnet run -- --save
```
Play an Existing Audio File:

```bash
dotnet run -- --playexisting "C:\path\to\file.mp3"
```
Display Help:

```bash
dotnet run -- --help
```

## Text-to-Speech API Container
This client is designed to work with a text-to-speech container that provides a TTS API endpoint. Configure the API URL in your `appsettings.json` file accordingly. The container handles converting text to speech and returns an MP3 file, which this client processes. I'm personally running this container `ghcr.io/matatonic/openedai-speech:latest`

## License
This project is licensed under the MIT License.

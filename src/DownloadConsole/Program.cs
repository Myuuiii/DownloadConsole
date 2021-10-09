using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using DownloadConsole.Models;
using Spectre.Console;

namespace DownloadConsole
{
	class Program
	{
		private static DCConfig _config;
		private static bool _stayOn = true;

		private const string _downloadSingleOption = "Download Single URL";
		private const string _downloadConfigOption = "Download using stored configuration";
		private const string _reloadConfigOption = "Reload configuration";
		private const string _customizeConfigurationOption = "[red]Edit configuration[/]";
		private const string _exitOption = "Exit";
		private static readonly List<string> AudioFormats = new List<string>{
			"mp3", "opus", "flac", "wav"
		};

		private static readonly List<string> VideoFormats = new List<string>{
			"mp4", "mkv", "mov", "avi"
		};

		static void Main(string[] args)
		{
			if (!File.Exists("config.json"))
			{
				AnsiConsole.Render(new Rule("Configuration").Alignment(Justify.Left));

				AnsiConsole.MarkupLine("[red]Config not found![/]");
				DCConfig config = new DCConfig();
				config.OutputDir = AnsiConsole.Ask<string>("Please specify the output directory path: ", "./");
				config.SourcesFile = AnsiConsole.Ask<string>("Please specify the input file path (optional): ", "");
				config.UseCustomThreads = AnsiConsole.Confirm("Would you like to use custom thread settings?", false);
				if (config.UseCustomThreads)
				{
					config.DownloadThreads = AnsiConsole.Ask<int>("How many threads would you like to assign for downloading: ");
					config.SearchThreads = AnsiConsole.Ask<int>("How many threads would you like yo assign for searching");
				}
				config.DownloadThumbnails = AnsiConsole.Confirm("Would you like to download the thumbnails for Soundcloud/YouTube downloads (separate picutre files)? ", false);
				config.AttatchThumbnails = AnsiConsole.Confirm("Would you like to attatch the thumbnails to SoundCloud/YouTube downloads", true);

				AnsiConsole.MarkupLine("[lime]Config file has been created[/]");

				File.WriteAllText("config.json", JsonSerializer.Serialize(config));
				_config = config;
			}
			else
			{
				_config = JsonSerializer.Deserialize<DCConfig>(File.ReadAllText("config.json"));
			}

			if (!Directory.Exists(_config.OutputDir))
				Directory.CreateDirectory(_config.OutputDir);

			AnsiConsole.Clear();

			while (_stayOn)
			{
				ShowConfig();
				string option = DrawMainMenu();
				switch (option)
				{
					case _downloadSingleOption:
						DownloadSingleMenu();
						break;
					case _downloadConfigOption:
						DownloadMultiMenu();
						break;
					case _reloadConfigOption:
						_config = JsonSerializer.Deserialize<DCConfig>(File.ReadAllText("config.json"));
						AnsiConsole.Clear();
						AnsiConsole.MarkupLine("[yellow]Configuration was reloaded[/]");
						Thread.Sleep(2000);
						break;
					case _customizeConfigurationOption:
						break;
					case _exitOption:
						_stayOn = false;
						break;
				}
			}
			AnsiConsole.Clear();
			AnsiConsole.MarkupLine("[aqua]Byebye![/]");
			Environment.Exit(0);
		}

		static string DrawMainMenu()
		{
			AnsiConsole.Render(new Rule("Main Menu").Alignment(Justify.Left));
			string option = AnsiConsole.Prompt(new SelectionPrompt<string>()
				.AddChoices(new[] {
					_downloadSingleOption,
					_downloadConfigOption,
					_reloadConfigOption,
					_customizeConfigurationOption,
					_exitOption
				 }).HighlightStyle(new Style(Color.White))
			);
			return option;
		}

		static void DownloadMultiMenu()
		{
			AnsiConsole.Clear();
			AnsiConsole.Render(new Rule("Downloading multiple URLs").Alignment(Justify.Left));
			string[] dataLines = File.ReadAllLines(_config.SourcesFile);
			int errors = 0;

			foreach (string dataLine in dataLines)
			{
				AnsiConsole.Clear();
				AnsiConsole.Render(new Rule("Downloading multiple URLs (In progress)").Alignment(Justify.Left));

				string sourceUrl = "";
				string targetFormat = "";
				if (dataLine.Split(' ').Length >= 2)
				{
					sourceUrl = dataLine.Split(' ')[0];
					targetFormat = dataLine.Split(' ')[1];
				}
				else continue;

				string folderName = "Single Item";
				if (dataLine.Split(' ').Length >= 3)
					folderName = string.Join(' ', dataLine.Split(' ').Skip(2));

				bool isRecognized = CheckUrlValidity(sourceUrl);
				UrlSource source = DetectUrlSource(sourceUrl);
				bool canDonwload = false;

				if (source == UrlSource.Spotify || source == UrlSource.Soundcloud)
				{
					if (AudioFormats.Contains(targetFormat))
					{ canDonwload = true; }
				}
				else if (source == UrlSource.YouTube)
				{
					if (AudioFormats.Contains(targetFormat) || VideoFormats.Contains(targetFormat))
					{ canDonwload = true; }
				}


				if (source == UrlSource.NoSource || isRecognized == false)
				{
					AnsiConsole.WriteLine("The source of this URL wasn't recognized");
					Thread.Sleep(1000);
					continue;
				}

				AnsiConsole.MarkupLine($"Format: [aqua]{targetFormat}[/]");
				ShowDownloadInformation();

				AnsiConsole.MarkupLine($"Current Item: [yellow]{folderName}[/]");

				// Start the download
				AnsiConsole.WriteLine();
				AnsiConsole.Render(new Rule("Download Output").Alignment(Justify.Left));
				if (Download(source, sourceUrl, targetFormat))
				{
					AnsiConsole.MarkupLine($"[lime] Download Succesful [/]: {sourceUrl}");
				}
				else
				{
					AnsiConsole.MarkupLine($"[red] Download Failed [/]: {sourceUrl}");
					errors++;
				}
				AnsiConsole.Render(new Rule("Download complete").Alignment(Justify.Left));
				Thread.Sleep(1000);
			}

			AnsiConsole.Clear();
			AnsiConsole.Render(new Rule("Downloading multiple URLs (Completed)").Alignment(Justify.Left));

			AnsiConsole.MarkupLine("[lime] Successfully completed queue [/]");

			if (errors > 0)
				AnsiConsole.MarkupLine($"In total, {errors} links could not be downloaded due to unknown problems");

			AnsiConsole.WriteLine("Press any key to go back to the main menu");
			Console.ReadKey();
		}

		static void DownloadSingleMenu()
		{
			AnsiConsole.Clear();
			AnsiConsole.Render(new Rule("Downloading single URL (In progress)").Alignment(Justify.Left));

			string url = AnsiConsole.Ask<string>("Please enter the URL: ");

			bool isRecognized = CheckUrlValidity(url);
			UrlSource source = DetectUrlSource(url);

			if (source == UrlSource.NoSource || isRecognized == false)
			{
				AnsiConsole.WriteLine("Press any key to return to the main menu...");
				Console.ReadKey();
				return;
			}

			string format = SelectFormat(source);
			AnsiConsole.MarkupLine($"Format: [aqua]{format}[/]");

			ShowDownloadInformation();

			// Start the download
			if (Download(source, url, format))
			{
				AnsiConsole.Clear();
				AnsiConsole.Render(new Rule("Downloading single URL (Completed)").Alignment(Justify.Left));
				AnsiConsole.MarkupLine("[lime] Download Succesful [/]");
				AnsiConsole.WriteLine("Press any key to return to the main menu...");
			}
			else
			{
				AnsiConsole.Clear();
				AnsiConsole.Render(new Rule("Downloading single URL (Failed)").Alignment(Justify.Left));
				AnsiConsole.MarkupLine("[red] Download Failed [/]");
				AnsiConsole.WriteLine("Press any key to return to the main menu...");
			}
			Console.ReadKey();
		}


		//
		//
		//
		// Functions
		//
		//
		//

		static void ShowDownloadInformation()
		{
			AnsiConsole.Markup($"Download Thumbail: ");
			if (_config.DownloadThumbnails)
				AnsiConsole.MarkupLine("[lime]Yes[/]");
			else
				AnsiConsole.MarkupLine("[red]No[/]");

			AnsiConsole.Markup($"Attatch Thumbail: ");
			if (_config.AttatchThumbnails)
				AnsiConsole.MarkupLine("[lime]Yes[/]");
			else
				AnsiConsole.MarkupLine("[red]No[/]");
		}

		static string SelectFormat(UrlSource source)
		{
			string format = "";
			SelectionPrompt<string> selectionPrompt = new SelectionPrompt<string>();
			selectionPrompt.Title("Please select a format");

			switch (source)
			{
				case UrlSource.Spotify:
				case UrlSource.Soundcloud:
					selectionPrompt.AddChoices(AudioFormats);
					break;
				case UrlSource.YouTube:
					selectionPrompt.AddChoices(VideoFormats);
					selectionPrompt.AddChoices(AudioFormats);
					break;
			}
			format = AnsiConsole.Prompt(selectionPrompt);

			return format;
		}
		static bool CheckUrlValidity(string url)
		{
			bool result = false;
			AnsiConsole.Status()
				.Start("Thinking...", ctx =>
				{
					ctx.Status = "Checking URL Validity";
					if (
						url.StartsWith("https://www.youtube.com")
						|| url.StartsWith("https://www.youtu.be")
						|| url.StartsWith("https://soundcloud.com/")
						|| url.StartsWith("https://www.soundcloud.com/")
						|| url.StartsWith("https://open.spotify.com/")
						)
					{
						result = true;
						AnsiConsole.MarkupLine(Environment.NewLine + "Validity: [lime]Valid[/]");
					}
					else
					{
						result = false;
						AnsiConsole.MarkupLine(Environment.NewLine + "Validity: [red]Invalid[/]");
					}
				});
			return result;
		}

		static UrlSource DetectUrlSource(string url)
		{
			UrlSource src = UrlSource.NoSource;
			if (url.StartsWith("https://www.youtube.com") || url.StartsWith("https://www.youtu.be")) src = UrlSource.YouTube;
			if (url.StartsWith("https://soundcloud.com/") || url.StartsWith("https://www.soundcloud.com/")) src = UrlSource.Soundcloud;
			if (url.StartsWith("https://open.spotify.com")) src = UrlSource.Spotify;

			switch (src)
			{
				case UrlSource.NoSource:
					AnsiConsole.MarkupLine("Source: [gray]Not Recognized[/]");
					break;
				case UrlSource.YouTube:
					AnsiConsole.MarkupLine("Source: [red]You[/][white]Tube[/]");
					break;
				case UrlSource.Soundcloud:
					AnsiConsole.MarkupLine("Source: [yellow]SoundCloud[/]");
					break;
				case UrlSource.Spotify:
					AnsiConsole.MarkupLine("Source: [lime]Spotify[/]");
					break;
			}

			return src;
		}

		static bool Download(UrlSource source, string url, string format)
		{
			AnsiConsole.Status()
			.Start("Downloading...", ctx =>
			{
				try
				{
					StringBuilder command = new StringBuilder();
					command.Append($"/c cd {_config.OutputDir} && ");

					switch (source)
					{
						case UrlSource.YouTube:
						case UrlSource.Soundcloud:
							command.Append($"youtube-dl -o %(title)s.%(ext)s --yes-playlist --audio-quality 0 --add-metadata ");

							if (AudioFormats.Contains(format))
							{
								command.Append($"--extract-audio --audio-format \"{format}\" ");
								if (_config.DownloadThumbnails)
									command.Append($"--write-thumbnail ");
								if (_config.AttatchThumbnails)
									command.Append($"--embed-thumbnail ");
							}

							if (VideoFormats.Contains(format))
								command.Append($"--format \"bestvideo+bestaudio[ext=m4a]/bestvideo+bestaudio/best\" --merge-output-format {format} ");

							command.Append(url);
							break;
						case UrlSource.Spotify:
							command.Append($"spotdl --output-format \"{format}\" ");
							if (_config.UseCustomThreads)
							{
								command.Append($"--download-threads {_config.DownloadThreads} ");
								command.Append($"--search-threads {_config.SearchThreads} ");
							}
							command.Append(url);
							break;
					}

					Process p = new Process();
					p.StartInfo.FileName = "cmd";
					p.StartInfo.Arguments = command.ToString();
					p.StartInfo.UseShellExecute = false;
					p.StartInfo.RedirectStandardError = true;
					p.StartInfo.RedirectStandardInput = true;
					p.StartInfo.RedirectStandardOutput = true;
					p.Start();

					p.WaitForExit();
					return true;
				}
				catch
				{
					return false;
				}
			});
			return true;
		}

		static void ShowConfig()
		{
			AnsiConsole.Clear();
			AnsiConsole.Render(new Rule("Configuration").Alignment(Justify.Left));
			Table table = new Table()
					.AddColumns(new[] { "Option", "Value" })
					.AddRow("Output Directory", _config.OutputDir)
					.AddRow("Sources File", _config.SourcesFile)
					.AddRow("Use Custom Thread Settings", _config.UseCustomThreads.ToString())
					.AddRow("Download Threads", _config.DownloadThreads.ToString())
					.AddRow("Search Threads", _config.SearchThreads.ToString())
					.AddRow("Download Thumbnails", _config.DownloadThumbnails.ToString())
					.AddRow("Attatch Thumbnails", _config.AttatchThumbnails.ToString())
					.Border(TableBorder.Rounded);
			AnsiConsole.Render(table);
			AnsiConsole.WriteLine();
		}
	}
}

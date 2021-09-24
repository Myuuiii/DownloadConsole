﻿using System;
using System.Collections.Generic;
using System.IO;
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
		private const string _configureOption = "Edit configuration";
		private const string _exitOption = "Exit";
		private readonly List<string> AudioFormats = new List<string>{
			"mp3", "opus", "flac", "wav"
		};

		private readonly List<string> VideoFormats = new List<string>{
			"mp4", "mkv", "mov", "avi"
		};

		static void Main(string[] args)
		{
			AnsiConsole.Render(new Rule("Checking Configuration").Alignment(Justify.Left));
			if (!File.Exists("config.json"))
			{
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

				AnsiConsole.MarkupLine("[lime]Config file has been created[/]");

				File.WriteAllText("config.json", JsonSerializer.Serialize(config));
				_config = config;
			}
			else
			{
				_config = JsonSerializer.Deserialize<DCConfig>(File.ReadAllText("config.json"));
				AnsiConsole.MarkupLine("[lime]Config found![/]");
				Table table = new Table()
					.AddColumns(new[] { "Option", "Value" })
					.AddRow("Output Directory", _config.OutputDir)
					.AddRow("Sources File", _config.SourcesFile)
					.AddRow("Use Custom Thread Settings", _config.UseCustomThreads.ToString())
					.AddRow("Download Threads", _config.DownloadThreads.ToString())
					.AddRow("Search Threads", _config.SearchThreads.ToString())
					.Border(TableBorder.Rounded);
				AnsiConsole.Render(table);
			}

			Thread.Sleep(1000);

			while (_stayOn)
			{
				string option = DrawMainMenu();
				switch (option)
				{
					case _downloadSingleOption:
						DownloadSingleMenu();
						break;
					case _downloadConfigOption:
						break;
					case _configureOption:
						break;
					case _exitOption:
						Environment.Exit(0);
						break;
				}
			}
		}

		static string DrawMainMenu()
		{
			AnsiConsole.Clear();
			AnsiConsole.Render(new Rule("Main Menu").Alignment(Justify.Left));
			string option = AnsiConsole.Prompt(new SelectionPrompt<string>()
				.AddChoices(new[] {
					_downloadSingleOption,
					_downloadConfigOption,
					_configureOption,
					_exitOption
				 })
			);
			return option;
		}

		static void DownloadSingleMenu()
		{
			AnsiConsole.Clear();
			AnsiConsole.Render(new Rule("Downloading single URL").Alignment(Justify.Left));

			string url = AnsiConsole.Ask<string>("Please enter the URL: ");

			bool isRecognized = false;
			isRecognized = CheckUrlValidity(url);

			if (!isRecognized)
			{
				AnsiConsole.MarkupLine(Environment.NewLine + "Validity: [red]Invalid[/]");
			}
			else
			{
				AnsiConsole.MarkupLine(Environment.NewLine + "Validity: [lime]Valid[/]");
			}

			UrlSource source = DetectUrlSource(url);
			switch (source)
			{
				case UrlSource.NoSource:
					AnsiConsole.MarkupLine("Source: [gray]Not Recognized[/]");
					break;
				case UrlSource.YouTube:
					AnsiConsole.MarkupLine("Source: [red]You[/][white]Tube[/]");
					break;
				case UrlSource.Soundcloud:
					AnsiConsole.MarkupLine("Source: [orange]SoundCloud[/]");
					break;
				case UrlSource.Spotify:
					AnsiConsole.MarkupLine("Source: [lime]Spotify[/]");
					break;
			}

			if (source == UrlSource.NoSource || isRecognized == false)
			{
				AnsiConsole.WriteLine("Since one or more of the checks failed you will be sent back to the main menu");
				Thread.Sleep(3000);
				return;
			}

			Console.ReadKey();
		}



		// Functions
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
					}
					else
					{
						result = false;
					}
				});
			return result;
		}

		static UrlSource DetectUrlSource(string url)
		{
			if (url.StartsWith("https://www.youtube.com") || url.StartsWith("https://www.youtu.be"))
				return UrlSource.YouTube;
			if (url.StartsWith("https://soundcloud.com/") || url.StartsWith("https://www.soundcloud.com/"))
				return UrlSource.Soundcloud;
			if (url.StartsWith("https://open.spotify.com"))
				return UrlSource.Spotify;

			return UrlSource.NoSource;
		}
	}
}

// See https://aka.ms/new-console-template for more information

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Text;

[assembly: AssemblyVersion ("1.0.*")]
var currentAssembly = Assembly.GetEntryAssembly () ?? Assembly.GetCallingAssembly ();

var verInfo = currentAssembly.GetName ();

AnsiConsole.MarkupLine ($"{verInfo.Name} [yellow]Version [italic]{verInfo.Version.Major}.{verInfo.Version.Minor}.{verInfo.Version.Build}[/][/]");


var app = new CommandApp<AbPropCommand> ();
return app.Run (args);

internal sealed class AbPropCommand : Command<AbPropCommand.Settings>
{
	public sealed class Settings : CommandSettings
	{
		[Description ("Path to search. Defaults to current directory.")]
		[CommandArgument (0, "[searchPath]")]
		public string? SearchPath { get; init; }

		[CommandOption ("--setalbum")]
		public string ForceAlbum { get; init; }

		[CommandOption ("--setauthor")]
		public string ForceAuthor { get; init; }

		[CommandOption ("--setreader")]
		public string ForceReader { get; init; }

		[CommandOption ("-f|--force")]
		[DefaultValue (false)]
		public bool ForceRename { get; init; }
	}

	public override int Execute ([NotNull] CommandContext context, [NotNull] Settings settings)
	{
		var searchPath = settings.SearchPath ?? Directory.GetCurrentDirectory ();

		var di = new DirectoryInfo (searchPath);
		var fc = di.GetFiles ("*.mp3");
		var format = fc.Length > 99 ? "000" : "00";
		var cnt = 0;
		var picname = Path.Combine (di.FullName, "folder.jpg");
		var dirname = "";
		var yr = (uint)0;

		var table = new Table { Border = TableBorder.Simple };
		table.AddColumn ("Filename");
		table.AddColumn ("#/#");
		table.AddColumn ("Artist");
		table.AddColumn ("Composer");
		table.AddColumn ("Album");
		table.AddColumn ("Year");
		table.AddColumn ("Title");



		foreach (var v in fc) {
			var tg = TagLib.File.Create (v.FullName);
			table.AddRow (
				v.Name,
				$"{tg.Tag.Track}/{tg.Tag.TrackCount}"
					, (string.IsNullOrEmpty (tg.Tag.FirstArtist) ? tg.Tag?.FirstAlbumArtist : tg.Tag?.FirstArtist)
			, tg.Tag?.FirstComposer ?? "",
			tg.Tag.Album,
			tg.Tag.Year.ToString ("D4"),
			tg.Tag.Title ?? ""

			);
			if (yr == 0 || tg.Tag.Year > 0) yr = tg.Tag.Year;



		}
		AnsiConsole.Write (table);
		if (yr.Equals (0)) {

			var rl = AnsiConsole.Ask<int> ("Album Year?");
			yr = (uint)rl;
		}
		table = new Table ().Centered ();
		table.Border = TableBorder.Simple;
		AnsiConsole.Live (table)
			.Start (ctx => {
				table.AddColumn ("Original");
				table.AddColumn ("#/#");
				table.AddColumn ("##/##");
				table.AddColumn ("New");

				foreach (var v in fc) {
					var tg = TagLib.File.Create (v.FullName);
					if (!File.Exists (picname)) {
						var firstpic = tg.Tag.Pictures.FirstOrDefault ();
						if (firstpic != null) WriteImage (firstpic.Data.Data, picname);
					}

					var artist = ConvertToValidFileName (tg.Tag.FirstArtist);


					if (string.IsNullOrEmpty (artist)) artist =
							ConvertToValidFileName (
						tg.Tag.AlbumArtists.FirstOrDefault (x => !string.IsNullOrEmpty (x)));

					if (!string.IsNullOrEmpty (settings.ForceAuthor)) artist = ConvertToValidFileName (settings.ForceAuthor);

					var composer =
						ConvertToValidFileName (
						tg.Tag.FirstComposer);

					if (!string.IsNullOrEmpty (settings.ForceReader)) composer = ConvertToValidFileName (settings.ForceReader);

					var album = tg.Tag.Album;
					if (!string.IsNullOrEmpty (settings.ForceAlbum)) album = ConvertToValidFileName (settings.ForceAlbum);



					if (string.IsNullOrEmpty (dirname)) {
						dirname = $"{artist} - {album} ({yr:0000})"
								.Replace (" :", ",")
								.Replace (":", ", ")
								.Replace ("\\", " ")
								.Replace ("/", " ")
								.Replace ("  ", " ")
							;

					}
					var tc = tg.Tag.TrackCount;



					string pattern = @"^Track\s\d+\s(of\s\d+\s)?\.mp3$";

					// Pattern to match just a number with .mp3 extension
					string numberPattern = @"^\d+\.mp3$";
					cnt++;
					var newname = $"Track {cnt.ToString (format)} of {fc.Length.ToString (format)}" +
								  v.Extension.ToLower ();
					table.AddRow (v.Name, tg.Tag.Track + "/" + tg.Tag.TrackCount,
						$" {cnt.ToString (format)}/{fc.Length.ToString (format)}", newname);
					ctx.Refresh ();
					tg.Tag.Track = (uint)cnt;
					tg.Tag.TrackCount = (uint)fc.Length;
					tg.Tag.Album = album;
					tg.Tag.Artists = new[] { artist };
					tg.Tag.AlbumArtists = new[] { artist };

					tg.Tag.Composers = new[] { composer };

					if (!yr.Equals (0)) tg.Tag.Year = yr;

					tg.Save ();

					if (Regex.IsMatch (v.Name, pattern) || Regex.IsMatch (v.Name, numberPattern) || settings.ForceRename) {
						newname = Path.Combine (v.DirectoryName, newname);
						if (File.Exists (newname)) continue;
						v.MoveTo (newname);
					}
				}
				ctx.Refresh ();

			});


		var vdi = Path.Combine (di.Parent.FullName, dirname);
		if (!Directory.Exists (vdi)) di.MoveTo (vdi);

		return 0;
	}
	public static string ConvertToValidFileName (string authorName)
	{
		if (string.IsNullOrEmpty (authorName)) return "";

		string cleanedName = Regex.Replace (authorName, @"[\p{P}\p{S}&&[^-]]", "");

		// Replace multiple spaces with a single space
		cleanedName = Regex.Replace (cleanedName, @"\s+", " ").Trim ();

		// Convert to UTF-8 encoding for filename (Windows uses UTF-16 for filenames)
		byte[] utf16Bytes = Encoding.Unicode.GetBytes (cleanedName);
		string utf16FileName = Encoding.Unicode.GetString (utf16Bytes);

		return utf16FileName;
	}

	void WriteImage (byte[] imageData, string fileName)
	{
		if (File.Exists (fileName)) return;
		try {
			using MemoryStream imageStream = new MemoryStream (imageData);
			using Image image = Image.Load (imageStream);

			int maxWidth = 500;
			int maxHeight = 500;

			if (image.Width > maxWidth || image.Height > maxHeight) {
				image.Mutate (x => x.Resize (new ResizeOptions {
					Mode = ResizeMode.Max,
					Size = new SixLabors.ImageSharp.Size (maxWidth, maxHeight)
				}));
			}

			// Save the image
			image.Save (fileName);
			//Console.WriteLine ($"Image written to: {fileName}");
		} catch (Exception ex) {
			//Console.WriteLine ($"Error: {ex.Message}");
		}
	}
}
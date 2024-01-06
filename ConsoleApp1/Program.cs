// See https://aka.ms/new-console-template for more information

using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

Console.WriteLine ("Hello, World!");

var di = new DirectoryInfo (@"Z:\audiobook\Jonathan Rugman - The Killing in the Consulate- The Life and Death of Jamal Khashoggi");
var fc = di.GetFiles ("*.m4b");
var format = fc.Length > 99 ? "000" : "00";
var cnt = 0;
var picname = Path.Combine (di.FullName, "folder.jpg");
var dirname = "";
var yr = (uint)0;
foreach (var v in fc) {
	var tg = TagLib.File.Create (v.FullName);
	if (!File.Exists (picname)) {
		var firstpic = tg.Tag.Pictures.FirstOrDefault ();
		if (firstpic != null) WriteImage (firstpic.Data.Data, picname);
	}

	var artist = tg.Tag.FirstAlbumArtist;
	var composer = tg.Tag.FirstComposer;

	if (string.IsNullOrEmpty (artist)) artist = tg.Tag.FirstArtist;
	if (string.IsNullOrEmpty (artist)) artist = tg.Tag.AlbumArtists.FirstOrDefault (x => !string.IsNullOrEmpty (x));
	if (yr == 0 || tg.Tag.Year > 0) yr = tg.Tag.Year;


	if (yr.Equals (0)) {
		Console.WriteLine ("Year? ");
		var rl = Console.ReadLine ();
		yr = uint.TryParse (rl, out var rn) ? rn : ((uint)0);
	}

	if (string.IsNullOrEmpty (dirname)) {
		dirname = $"{tg.Tag.AlbumArtists.FirstOrDefault ()} - {tg.Tag.Album} ({yr:0000})"
				.Replace (" :", ",")
				.Replace (":", ", ")
				.Replace ("\\", " ")
				.Replace ("/", " ")
				.Replace ("  ", " ")
			;

	}
	var tc = tg.Tag.TrackCount;

	Console.Write (v.Name + " " + tg.Tag.Track + "/" + tg.Tag.TrackCount);
	string pattern = @"^Track\s\d+\s(of\s\d+\s)?\.mp3$";

	// Pattern to match just a number with .mp3 extension
	string numberPattern = @"^\d+\.mp3$";
	cnt++;
	tg.Tag.Track = (uint)cnt;
	tg.Tag.TrackCount = (uint)fc.Length;

	if (!tg.Tag.AlbumArtists.Any () && !string.IsNullOrEmpty (artist)) tg.Tag.AlbumArtists = new[] { artist };
	if (!tg.Tag.Composers.Any () && !string.IsNullOrEmpty (composer)) tg.Tag.Composers = new[] { composer };
	if (!yr.Equals (0)) tg.Tag.Year = yr;
	tg.Save ();
	Console.WriteLine ($" {cnt.ToString (format)}/{fc.Length.ToString (format)}");
	if (Regex.IsMatch (v.Name, pattern) || Regex.IsMatch (v.Name, numberPattern)) {
		var newname = $"Track {tg.Tag.Track.ToString (format)} of {tg.Tag.TrackCount.ToString (format)}" +
					  v.Extension.ToLower ();
		newname = Path.Combine (v.DirectoryName, newname);
		if (File.Exists (newname)) continue;
		v.MoveTo (newname);
	}
}

var vdi = Path.Combine (di.Parent.FullName, dirname);
if (!Directory.Exists (vdi)) di.MoveTo (vdi);

return;

static void WriteImage (byte[] imageData, string fileName)
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
				Size = new Size (maxWidth, maxHeight)
			}));
		}

		// Save the image
		image.Save (fileName);
		Console.WriteLine ($"Image written to: {fileName}");
	} catch (Exception ex) {
		Console.WriteLine ($"Error: {ex.Message}");
	}
}
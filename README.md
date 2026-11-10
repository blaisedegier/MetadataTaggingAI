# MetadataTaggingAI

A .NET console tool that keywords a folder of photos from their folder names and Azure AI Vision, and writes the keywords into each file.

[![CI](https://github.com/blaisedegier/MetadataTaggingAI/actions/workflows/ci.yml/badge.svg)](https://github.com/blaisedegier/MetadataTaggingAI/actions/workflows/ci.yml)

## What it does

It is meant for photo libraries where photographers already sort shoots into folders such as
`kruger-2024/Big-Cats/`. For every image under a root folder it:

- turns the root folder's name and every folder below it into keywords (`Kruger`, `2024`, `Big`, `Cats`),
- asks Azure AI Vision what is in the picture (`Lion`, `Grass`, `Wildlife`),
- merges the two lists, drops case-insensitive duplicates, and writes the result into the file as
  IPTC Keywords and XMP `dc:subject`, so photo managers that read either field can search on them.

By default the keywords are written into the original files. `--output <folder>` instead writes a
tagged copy of each image to that folder, in the same folder layout, and only reads the originals.
A `--dry-run` prints the keywords each image would get and writes nothing. It still calls Azure
when a key is configured.

Two small tools sit beside it:

- `tools/MetadataInspector` prints the IPTC Keywords and XMP `dc:subject` already stored in an image.
- `tools/DropboxSharedLinkDownloader` downloads everything behind a Dropbox shared folder link,
  keeping the folder structure, so a shoot delivered by link can be tagged locally.

## How it works

`Program` walks the root folder recursively and, for each file `FolderTagExtractor.IsImageFile`
accepts (`.jpg`, `.jpeg`, `.png`, `.tiff`), collects tags from two sources:

- `FolderTagExtractor.GetFolderHierarchyTags` takes the path relative to the root, prepends the
  root folder's own name, splits each folder name on `-`, `+`, `_` and `.`, and title-cases the
  parts with the invariant culture so results do not depend on the machine's locale. Spaces are
  kept, so `big cats` stays one tag.
- `AzureVisionTagger` sends the image to Image Analysis 4.0 with the `Tags` feature. One
  `ImageAnalysisClient` is created per run and reused for every image.

`ImageMetadataWriter` then opens the image with Magick.NET, clears the existing IPTC Keyword values
and the existing `rdf:Bag` under XMP `dc:subject`, writes the merged list into both, and saves the
result to a temporary file in the same folder. Only once that save has succeeded is the temporary
file moved over the target (`File.Replace`), so a crash or a full disk part-way through leaves the
original intact rather than truncated. XMP is edited as an `XDocument`; an image with no XMP gets a
new packet with an `rdf:Description` to hold the subjects.

If Azure or the write fails for an image, the error is printed, that file is left as it was, and the
run carries on; the exit code is non-zero when anything was skipped.

## Running it

Prerequisites: the .NET 10 SDK, and an Azure AI Vision (Computer Vision) resource for its endpoint
and key. A dry run works without Azure and shows folder tags only.

Configuration is read from `appsettings.json` beside the executable, then from environment variables
of the same names. Copy the example and fill it in:

```bash
cp src/MetadataTaggingAI/appsettings.example.json src/MetadataTaggingAI/appsettings.json
```

| Key           | Meaning                                                                    |
| ------------- | -------------------------------------------------------------------------- |
| `ApiKey`      | Azure AI Vision key                                                        |
| `ApiEndpoint` | Azure AI Vision endpoint, e.g. `https://name.cognitiveservices.azure.com/` |
| `ImageRoot`   | Folder to tag when none is given on the command line                       |

`appsettings.json` is gitignored. The root folder comes from the first argument, then `ImageRoot`,
then `./Images`. Point it at the shoot folder itself, because that folder's name becomes a tag.

```bash
dotnet build MetadataTaggingAI.slnx
dotnet test MetadataTaggingAI.slnx

# See what each image would get, without touching any file
dotnet run --project src/MetadataTaggingAI -- "/photos/kruger-2024" --dry-run

# Write tagged copies to another folder (needs ApiKey and ApiEndpoint); the originals are only read
dotnet run --project src/MetadataTaggingAI -- "/photos/kruger-2024" --output "/photos/tagged/kruger-2024"

# Check what landed in a file
dotnet run --project tools/MetadataInspector -- "/photos/tagged/kruger-2024/Big-Cats/lion.jpg"

# Write the keywords into the originals themselves
dotnet run --project src/MetadataTaggingAI -- "/photos/kruger-2024"
```

Prefer `--output`. Writing into the originals replaces their keywords and re-encodes them (see
below), so back the folder up first if you do. The output folder must be outside the root folder,
so the copies are never picked up by a later run.

The Dropbox downloader takes `AccessToken`, `SharedLink` and `OutputDirectory` from its own
`appsettings.json` (see `tools/DropboxSharedLinkDownloader/appsettings.example.json`) or the
environment; the link and the output folder can also be passed as arguments. Output defaults to
`./dropbox-download`.

```bash
dotnet run --project tools/DropboxSharedLinkDownloader -- "https://www.dropbox.com/scl/fo/..." ./kruger-2024
```

The tests cover folder tag extraction, image detection, the IPTC and XMP updates, and a round trip
that writes keywords into a generated JPEG and reads them back. None of them call Azure or Dropbox.

## Tech

.NET 10, Azure.AI.Vision.ImageAnalysis, Magick.NET, Microsoft.Extensions.Configuration, Dropbox.Api, xUnit.

## Known limitations

- **Existing keywords are replaced, not merged.** Any IPTC Keywords or XMP `dc:subject` entries a
  photographer added by hand are dropped from the written file. Run a `--dry-run` first, and use
  `--output` or keep a copy of the originals.
- **Files are re-encoded.** Magick.NET decodes and re-saves each image, so JPEGs go through a
  second lossy compression and the file bytes change even where the picture looks the same.
  Magick.NET has no way to rewrite only a JPEG's metadata segments (its lossless `JpegOptimizer`
  recompresses the existing data but cannot add profiles), so avoiding this would need a separate
  metadata library or ExifTool.
- Only JPEG has been exercised end to end. Whether IPTC and XMP survive in PNG and TIFF depends on
  what ImageMagick writes for each format. GIF and BMP files are skipped, because BMP has nowhere to
  store keywords and re-saving an animated GIF would keep only its first frame.
- Compatibility with Lightroom, Bridge or other photo managers has not been tested.
- Writes need an Azure AI Vision resource, and every image is one billable call. Tags are requested in
  the service's default language (English), and no confidence threshold is applied.
- Images are processed one at a time. Azure calls get only the Azure SDK's default retry policy,
  with no extra handling for throttling.
- The Dropbox downloader has not been run against a live shared link in its current form, needs a
  user access token rather than an OAuth flow, and re-downloads everything on each run.

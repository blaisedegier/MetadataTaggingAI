# MetadataTaggingAI 🏷️

Automatically tag images with rich, searchable keywords by combining **folder-based semantics** with **Azure AI Vision** — then embed those keywords directly into industry-standard **IPTC** and **XMP** metadata.

Built for photographers, digital asset managers, and anyone who wants their images to be instantly discoverable without hours of manual keywording.

## Table of Contents

- [How It Works](#how-it-works)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Running](#running)
- [Running Tests](#running-tests)
- [Supported Formats](#supported-formats)
- [Demo Projects](#demo-projects)

## How It Works

```text
┌───────────────────┐     ┌───────────────────┐     ┌───────────────────┐
│  Folder Hierarchy │     │  Azure AI Vision  │     │  Image Metadata   │
│                   │     │                   │     │                   │
│  /Animals/        │────>│  Analyze image    │────>│  IPTC Keywords    │
│   Big-Cats/       │     │  for visual tags  │     │  XMP dc:subject   │
│    lion.jpg       │     │                   │     │                   │
└───────────────────┘     └───────────────────┘     └───────────────────┘
   Tags: "Animals",        Tags: "Lion",              All tags merged &
   "Big", "Cats"           "Wildlife", "Cat"          written into file
```

1. **Folder hierarchy extraction:** Directory names in the image path are parsed into tags. Separators like `-`, `+`, `_`, `.` are split and each segment is title-cased.
2. **Azure AI Vision analysis:** Each image is sent to Azure's Image Analysis API which returns content-based visual tags (objects, scenes, concepts).
3. **Tag merging:** Folder-derived and AI-generated tags are combined and deduplicated.
4. **Metadata embedding:** The final tag set is written into the image file as both **IPTC Keywords** and **XMP Dublin Core subjects** (`dc:subject`), using Magick.NET with full profile preservation.

## Features

- **Dual tagging strategy:** Combines organizational context (folder names) with AI-detected visual content for richer, more accurate keywords.
- **Industry-standard metadata:** Writes both IPTC Keywords and XMP `dc:subject` tags, ensuring compatibility with Adobe Lightroom, Bridge, Capture One, and other DAM tools.
- **Non-destructive writes:** Uses Magick.NET's profile preservation to keep existing metadata (EXIF, color profiles, etc.) intact when writing tags.
- **Recursive processing:** Processes entire folder trees in a single run.
- **Smart text normalization:** Handles mixed case, special characters, and separator conventions (kebab-case, snake_case, etc.) and normalizes to Title Case.
- **Deduplication:** Prevents duplicate keywords via case-insensitive comparison.

## Tech Stack

| Component            | Technology                                                                                                                                                              |
| -------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Runtime              | .NET 8 / C# 12                                                                                                                                                          |
| AI Vision            | [Azure AI Vision — Image Analysis](https://learn.microsoft.com/en-us/azure/ai-services/computer-vision/overview-image-analysis) (`Azure.AI.Vision.ImageAnalysis` 1.0.0) |
| Image I/O & Metadata | [Magick.NET](https://github.com/dlemstra/Magick.NET) (Q16-AnyCPU 14.4.0)                                                                                                |
| XMP Handling         | `System.Xml.Linq` (built-in)                                                                                                                                            |
| Testing              | xUnit 2.9.3                                                                                                                                                             |
| Configuration        | `Microsoft.Extensions.Configuration.Json`                                                                                                                               |

## Project Structure

```text
MetadataTaggingAI/
├── PrototypeV1/
│   ├── PrototypeV1.sln                 # Main solution
│   ├── PrototypeV1/
│   │   ├── PrototypeV1.csproj          # Console app project
│   │   ├── Program.cs                  # ImageProcessor — core tagging pipeline
│   │   └── ImageMetadataWriter.cs      # IPTC + XMP metadata writer
│   └── ImageProcessorTests/
│       ├── ImageProcessorTests.csproj   # Test project
│       └── UnitTest.cs                  # xUnit tests for tagging & metadata
├── Demo/                                # Standalone experiments & utilities
│   ├── Tester/                          # Azure Image Analysis tag listing
│   ├── XMP/                             # XMP/IPTC metadata reader
│   ├── Dropbox/                         # Dropbox shared folder downloader
│   └── Demo/                            # Dropbox API exploration
├── .gitignore
└── README.md
```

### Key Files

| File                                 | Responsibility                                                                                         |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------ |
| `PrototypeV1/Program.cs`             | Entry point & orchestration — scans folders, calls Azure Vision, merges tags, triggers metadata writes |
| `PrototypeV1/ImageMetadataWriter.cs` | Reads/writes IPTC and XMP metadata using Magick.NET with full profile preservation                     |
| `ImageProcessorTests/UnitTest.cs`    | Unit tests for folder tag extraction, image file detection, IPTC updates, and XMP profile creation     |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- An **Azure AI Services** resource with the **Computer Vision / Image Analysis** API enabled
  - You'll need the **endpoint URL** and an **API key** ([Quickstart guide](https://learn.microsoft.com/en-us/azure/ai-services/computer-vision/quickstarts-sdk/image-analysis-client-library))

## Getting Started

1. **Clone the repository**

   ```bash
   git clone https://github.com/<your-username>/MetadataTaggingAI.git
   cd MetadataTaggingAI
   ```

2. **Restore dependencies**

   ```bash
   dotnet restore PrototypeV1/PrototypeV1.sln
   ```

3. **Create your configuration file**

   Create `PrototypeV1/PrototypeV1/appsettings.json` (this file is git-ignored for security):

   ```json
   {
     "ApiKey": "<your-azure-vision-api-key>",
     "ApiEndpoint": "https://<your-resource-name>.cognitiveservices.azure.com/"
   }
   ```

4. **Add images**

   Place your image folders under an `Images/` directory (also git-ignored). The tool processes the first subdirectory it finds, so structure it like:

   ```text
   Images/
   └── MyCollection/
       ├── Animals/
       │   ├── Big-Cats/
       │   │   └── lion.jpg
       │   └── Birds/
       │       └── eagle.png
       └── Landscapes/
           └── sunset.jpg
   ```

## Configuration

The app reads from `appsettings.json` at runtime:

| Key           | Description                  | Example                                           |
| ------------- | ---------------------------- | ------------------------------------------------- |
| `ApiKey`      | Azure AI Vision API key      | `a1b2c3d4e5f6...`                                 |
| `ApiEndpoint` | Azure AI Vision endpoint URL | `https://myresource.cognitiveservices.azure.com/` |

The target image directory is configured via the `TARGET_DIR` constant in `Program.cs` (defaults to `~/Images`).

## Running

```bash
dotnet run --project PrototypeV1/PrototypeV1/PrototypeV1.csproj
```

The app will:

1. Scan the configured image directory recursively
2. For each image, extract tags from the folder hierarchy
3. Analyze each image with Azure AI Vision to get visual tags
4. Merge all tags and write them as IPTC Keywords and XMP `dc:subject` into the image file
5. Log progress to the console

## Running Tests

```bash
dotnet test PrototypeV1/PrototypeV1.sln
```

The test suite covers:

- **Folder hierarchy tag extraction:** nested folders, mixed case, separators, edge cases
- **Image file detection:** supported vs. unsupported extensions
- **IPTC keyword management:** adding, replacing, and clearing keywords
- **XMP profile handling:** creating new profiles and updating existing ones

## Supported Formats

| Format | Extension       | Supported |
| ------ | --------------- | --------- |
| JPEG   | `.jpg`, `.jpeg` | Yes       |
| PNG    | `.png`          | Yes       |
| GIF    | `.gif`          | Yes       |
| TIFF   | `.tiff`         | Yes       |
| BMP    | `.bmp`          | Yes       |
| WebP   | `.webp`         | No        |

## Demo Projects

The `Demo/` folder contains standalone experiments used during development:

| Project     | Description                                                        |
| ----------- | ------------------------------------------------------------------ |
| **Tester**  | Lists Azure Image Analysis tags for images in a folder             |
| **XMP**     | Reads and displays existing XMP and IPTC metadata from image files |
| **Dropbox** | Downloads images recursively from a Dropbox shared link            |
| **Demo**    | Dropbox API exploration and authentication testing                 |

Each demo has its own `.sln` and can be run independently.

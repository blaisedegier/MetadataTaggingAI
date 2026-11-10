namespace DropboxSharedLinkDownloader
{
    // Dropbox allows names that Windows cannot store, so every name is made safe before it touches
    // the disk. The Windows rules are applied on every OS, so a download behaves the same wherever
    // it runs and can later be copied to a Windows machine.
    public static class LocalFileName
    {
        private static readonly HashSet<char> InvalidChars = new(
            @"<>:""/\|?*"
                .Concat(Enumerable.Range(0, 32).Select(i => (char)i))
                .Concat(Path.GetInvalidFileNameChars()));

        private static readonly HashSet<string> ReservedNames = new(
            new[] { "CON", "PRN", "AUX", "NUL" }
                .Concat(Enumerable.Range(1, 9).Select(i => $"COM{i}"))
                .Concat(Enumerable.Range(1, 9).Select(i => $"LPT{i}")),
            StringComparer.OrdinalIgnoreCase);

        // Replaces invalid characters with '_', drops trailing dots and spaces (Windows strips them
        // silently, so "photo." and "photo" would land on the same file), and prefixes device names
        // such as CON or nul.txt, which Windows reserves whatever the extension.
        public static string Sanitise(string name)
        {
            var result = new string(name.Select(c => InvalidChars.Contains(c) ? '_' : c).ToArray())
                .TrimEnd('.', ' ');

            if (result.Length == 0)
                return "_";

            var stem = result.Split('.')[0].TrimEnd(' ');
            return ReservedNames.Contains(stem) ? "_" + result : result;
        }
    }
}

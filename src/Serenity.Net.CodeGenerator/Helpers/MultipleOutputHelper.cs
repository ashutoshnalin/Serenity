namespace Serenity.CodeGenerator;

public class MultipleOutputHelper
{
    private static readonly Encoding utf8 = new UTF8Encoding(true);

    public static void WriteFiles(IFileSystem fileSystem,
        string outDir, IEnumerable<(string Path, string Text)> filesToWrite,
        string[] deleteExtraPattern,
        string endOfLine)
    {
        ArgumentExceptionHelper.ThrowIfNull(fileSystem);

        outDir = fileSystem.GetFullPath(outDir);
        fileSystem.CreateDirectory(outDir);

        var generated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, txt) in filesToWrite)
        {
            generated.Add(PathHelper.ToUrl(path));

            var outFile = fileSystem.Combine(outDir, path);
            bool exists = fileSystem.FileExists(outFile);
            if (exists)
            {
                var content = fileSystem.ReadAllText(outFile, utf8);
                if (content.Trim().Replace("\r", "", StringComparison.Ordinal) ==
                    (txt ?? "").Trim().Replace("\r", "", StringComparison.Ordinal))
                    continue;
            }
            else if (!fileSystem.DirectoryExists(fileSystem.GetDirectoryName(outFile)))
                fileSystem.CreateDirectory(fileSystem.GetDirectoryName(outFile));

#if !ISSOURCEGENERATOR
            Console.ForegroundColor = exists ? ConsoleColor.Magenta : ConsoleColor.Green;
            Console.Write(exists ? "Overwriting: " : "New File: ");
            Console.ResetColor();
            Console.WriteLine(fileSystem.GetFileName(outFile));
#endif

            string text = txt ?? "";
            if (string.Equals(endOfLine, "lf", StringComparison.OrdinalIgnoreCase))
                text = text.Replace("\r", "");
            else if (string.Equals(endOfLine, "crlf", StringComparison.OrdinalIgnoreCase))
                text = text.Replace("\r", "").Replace("\n", "\r\n");

            fileSystem.WriteAllText(outFile, text, utf8);
        }

        if (deleteExtraPattern?.Length is null or 0)
            return;

        var filesToDelete = deleteExtraPattern.SelectMany(
                x => fileSystem.GetFiles(outDir, x, recursive: true))
            .Distinct();

        var outRoot = PathHelper.ToUrl(outDir).TrimEnd('/') + '/';
        foreach (var file in filesToDelete)
            if (PathHelper.ToUrl(file).StartsWith(outRoot) &&
                !generated.Contains(PathHelper.ToUrl(file)[outRoot.Length..]))
            {
#if !ISSOURCEGENERATOR
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Deleting: ");
                Console.ResetColor();
                Console.WriteLine(fileSystem.GetFileName(file));
#endif
                fileSystem.DeleteFile(file);
            }
    }
}
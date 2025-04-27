using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JournalParser;

public class Parser
{
    private readonly string _inputFilePath;
    private readonly string _outputDirectory;

    public Parser(string inputFilePath, string outputDirectory)
    {
        _inputFilePath = inputFilePath;
        _outputDirectory = outputDirectory;
    }

    public void ParseAndWriteEntries()
    {
        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
        }

        string content = File.ReadAllText(_inputFilePath);

        var entryPattern = new Regex(@"\tDate:\t(?<date>.+?)\r?\n(?<body>(.*?))(?=(\tDate:\t|\z))", RegexOptions.Singleline);

        var matches = entryPattern.Matches(content);

        var entriesByMonth = new Dictionary<string, List<string>>();

        foreach (Match match in matches)
        {
            string dateText = match.Groups["date"].Value.Trim();
            string body = match.Groups["body"].Value.Trim();

            DateTime parsedDate = ParseDate(dateText);
            string formattedDate = parsedDate.ToString("yyyy-MM-dd");
            string displayDate = parsedDate.ToString("d MMMM yyyy");
            string weight = parsedDate.ToString("yyMMdd");
            string dayOfMonth = parsedDate.Day.ToString();
            string monthFolder = parsedDate.ToString("yyyy-MM");
            string monthFolderPath = Path.Combine(_outputDirectory, monthFolder);

            if (!Directory.Exists(monthFolderPath))
            {
                Directory.CreateDirectory(monthFolderPath);
            }

            string title = ExtractTitle(body) ?? "Untitled";

            // Insert the date after the heading
            body = InsertDateAfterHeading(body, displayDate);

            string frontMatter = $"---\n" +
                                 $"title: \"{dayOfMonth}. {EscapeQuotes(title)}\"\n" +
                                 $"date: {formattedDate}\n" +
                                 $"weight: {weight}\n" +
                                 $"---\n\n";

            string safeDate = parsedDate.ToString("yyyyMMdd");
            string outputFileName = $"{safeDate}.md";
            string outputFilePath = Path.Combine(monthFolderPath, outputFileName);

            File.WriteAllText(outputFilePath, frontMatter + body, Encoding.UTF8);
            Console.WriteLine($"Written: {outputFilePath}");

            if (!entriesByMonth.ContainsKey(monthFolder))
            {
                entriesByMonth[monthFolder] = new List<string>();
            }
            entriesByMonth[monthFolder].Add(outputFileName);
        }

        foreach (var kvp in entriesByMonth)
        {
            string monthFolderPath = Path.Combine(_outputDirectory, kvp.Key);
            DateTime anyDate = ParseDateFromFolderName(kvp.Key);
            string monthYearTitle = anyDate.ToString("MMMM yyyy");
            string monthWeight = anyDate.ToString("yyMM");

            StringBuilder indexContent = new StringBuilder();
            indexContent.AppendLine("---");
            indexContent.AppendLine($"title: \"{monthYearTitle}\"");
            indexContent.AppendLine("bookCollapseSection: true");
            indexContent.AppendLine($"weight: {monthWeight}");
            indexContent.AppendLine("---\n");

            foreach (var page in kvp.Value.OrderBy(x => x))
            {
                indexContent.AppendLine($"{{{{< relref \"{page}\" >}}}}");
            }

            string indexPath = Path.Combine(monthFolderPath, "_index.md");
            File.WriteAllText(indexPath, indexContent.ToString(), Encoding.UTF8);
            Console.WriteLine($"Written index: {indexPath}");
        }
    }

    private string ExtractTitle(string body)
    {
        var titlePattern = new Regex(@"^#\s*(?<title>.+)$", RegexOptions.Multiline);
        var match = titlePattern.Match(body);
        return match.Success ? match.Groups["title"].Value.Trim() : null;
    }

    private string InsertDateAfterHeading(string body, string displayDate)
    {
        var lines = body.Split(new[] { '\n' }, 2);
        if (lines.Length == 2)
        {
            return lines[0] + "\n\n" + displayDate + "\n\n" + lines[1];
        }
        return body;
    }

    private string EscapeQuotes(string input)
    {
        return input.Replace("\"", "\\\"");
    }

    private DateTime ParseDate(string dateText)
    {
        int atIndex = dateText.IndexOf(" at ");
        if (atIndex >= 0)
        {
            dateText = dateText.Substring(0, atIndex);
        }

        if (DateTime.TryParse(dateText, out DateTime result))
        {
            return result;
        }
        throw new FormatException($"Could not parse date: {dateText}");
    }

    private DateTime ParseDateFromFolderName(string folderName)
    {
        if (DateTime.TryParseExact(folderName, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
        {
            return result;
        }
        throw new FormatException($"Could not parse folder name: {folderName}");
    }
}
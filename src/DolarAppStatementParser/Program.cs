using System.Globalization;
using System.Text;

const string statementsFileExtension = ".pdf";
const string statementsFileNamePrefix = "DolarApp Statement - ";

var directoryPath = args.Length > 0 ? args[0] : Environment.CurrentDirectory;
Console.WriteLine($"Looking for DolarApp statement files in {directoryPath}...");
var files = Directory.GetFiles(directoryPath);
var statementFiles = files.Where(n => n.EndsWith(statementsFileExtension) && n.Contains(statementsFileNamePrefix)).ToArray();
if (statementFiles.Length == 0)
{
    Console.WriteLine("No DolarApp statement files found.");
    return;
}

using var streamWriter = new StreamWriter( $"{directoryPath}\\MyStatement{DateTime.Now:yyyyMMdd_HHmmss}.csv", false, Encoding.UTF8);
streamWriter.WriteLine("Date,Type,Amount,Currency,LocalAmount,Description,Flow");
foreach (var file in statementFiles)
{
    if (!DateOnly.TryParse(
            file.Substring(
                file.IndexOf(statementsFileNamePrefix, StringComparison.InvariantCulture) +
                statementsFileNamePrefix.Length, 7), out var fileNameDate))
    {
        Console.WriteLine($"Skipping file {file} as the year seems wrong.");
        continue;
    }
    
    var year = fileNameDate.AddMonths(1).Year;
    if (year < 2023 || year > DateTime.Now.Year)
    {
        Console.WriteLine($"Skipping file {file} as the year seems wrong.");
        continue;
    }
    Console.WriteLine($"Processing file {file} for year {year}");
    ProcessFile(file, year, streamWriter);
}
return;

static void ProcessFile(string file, int year, StreamWriter streamWriter)
{
    const string textForRefund = "Devolución de tarjeta";
    const string lineSeparator = "\r\n";
    const int movementLineLength = 92;
    const int movementLineDateColumnLength = 7;
    const int movementLineTypeColumnLength = 24;
    const int movementLineAmountColumnLength = 18;
    const int movementLineCurrencyColumnLength = 16;
    const int movementLineLocalAmountColumnLength = 17;
    const string plusSignWithASpace = "+ ";
    const string minusSignWithASpace = "- ";
    const string inFlow = "In";
    const string outFlow = "Out";
    var amountsFormat = new NumberFormatInfo
    {
        NumberGroupSeparator = ",",
        NumberDecimalSeparator = "."
    };
    using var document = new Aspose.Pdf.Document(file);
    var textAbsorber = new Aspose.Pdf.Text.TextAbsorber();
    document.Pages.Accept(textAbsorber);
    var extractedText = textAbsorber.Text;
    var lines = extractedText.Split(lineSeparator);
    foreach (var line in lines.Where(l=>l.Length > movementLineLength).Select(t=>t.TrimStart()))
    {
        if (!DateOnly.TryParse(line.Remove(movementLineDateColumnLength) + " " + year, out var date)) continue;
        var remainingTextAfterRemovingDate = line.Substring(movementLineDateColumnLength).TrimStart();
        var txType = remainingTextAfterRemovingDate.Remove(movementLineTypeColumnLength).TrimEnd();
        var remainingTextAfterRemovingType = remainingTextAfterRemovingDate.Substring(movementLineTypeColumnLength).TrimStart();
        var amount = double.Parse(remainingTextAfterRemovingType.Remove(movementLineAmountColumnLength)
            .TrimEnd()
            .Replace(plusSignWithASpace, string.Empty)
            .Replace(minusSignWithASpace, "-"), amountsFormat);
        var remainingTextAfterRemovingAmount = remainingTextAfterRemovingType.Substring(movementLineAmountColumnLength).TrimStart();
        var currency = remainingTextAfterRemovingAmount.Remove(movementLineCurrencyColumnLength).TrimEnd();
        double? localAmount = null;
        var remainingTextAfterRemovingCurrency = remainingTextAfterRemovingAmount.Substring(movementLineCurrencyColumnLength).TrimStart();
        if (currency != "N/A")
            localAmount = double.Parse(remainingTextAfterRemovingCurrency.Remove(movementLineLocalAmountColumnLength)
                .TrimEnd()
                .Replace(plusSignWithASpace, string.Empty)
                .Replace(minusSignWithASpace, "-"),amountsFormat);
        var remainingTextAfterRemovingLocalAmount = remainingTextAfterRemovingCurrency.Substring(movementLineLocalAmountColumnLength).TrimStart();
        var description = remainingTextAfterRemovingLocalAmount.TrimEnd();
        var flow = amount > 0 ? inFlow : outFlow;
        if(string.IsNullOrWhiteSpace(txType) && flow == inFlow) txType = textForRefund;
        streamWriter.WriteLine($"{date:yyyy-MM-dd},\"{txType}\",{amount},{currency},{localAmount},\"{description}\",{flow}");
    }
}
using ClosedXML.Excel;

namespace RallyAPI.SharedKernel.Utilities.Payouts;

/// <summary>
/// One beneficiary row in an ICICI bulk-transfer export file. Shared by the restaurant and
/// rider export commands — both produce this shape, then hand it to
/// <see cref="IciciBulkTransferExcelWriter"/> to render the actual .xlsx.
/// </summary>
public sealed record IciciExportRow(
    string BeneficiaryName,
    string AccountNumber,
    string IfscCode,
    decimal Amount,
    string Narration);

/// <summary>
/// Renders an ICICI bulk-transfer .xlsx from a set of beneficiary rows. The column layout
/// here is a placeholder — swap it for ICICI's real bulk-upload template once the bank
/// provides it. This is the ONLY place that layout should need to change; callers only ever
/// deal in <see cref="IciciExportRow"/>.
/// </summary>
public static class IciciBulkTransferExcelWriter
{
    private static readonly string[] Headers =
    {
        "Beneficiary Name", "Account Number", "IFSC Code", "Amount", "Narration"
    };

    /// <summary>
    /// Writes the workbook and returns the raw bytes. The caller is responsible for
    /// persisting/streaming these bytes and for hashing them (SHA-256) into the
    /// export batch's audit trail.
    /// </summary>
    public static byte[] Write(IReadOnlyList<IciciExportRow> rows, string sheetTitle)
    {
        if (rows.Count == 0)
            throw new ArgumentException("Cannot write an export file with zero rows.", nameof(rows));

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetTitle);

        for (var col = 0; col < Headers.Length; col++)
        {
            var cell = sheet.Cell(1, col + 1);
            cell.Value = Headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var row = 2;
        foreach (var r in rows)
        {
            sheet.Cell(row, 1).Value = r.BeneficiaryName;
            sheet.Cell(row, 2).Value = r.AccountNumber;
            sheet.Cell(row, 3).Value = r.IfscCode;
            sheet.Cell(row, 4).Value = r.Amount;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "0.00";
            sheet.Cell(row, 5).Value = r.Narration;
            row++;
        }

        // Control-sum total row — lets the operator (and ICICI's own validation) cross-check
        // the file total before/after upload.
        sheet.Cell(row, 3).Value = "Total";
        sheet.Cell(row, 3).Style.Font.Bold = true;
        var totalCell = sheet.Cell(row, 4);
        totalCell.FormulaA1 = $"=SUM(D2:D{row - 1})";
        totalCell.Style.NumberFormat.Format = "0.00";
        totalCell.Style.Font.Bold = true;

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

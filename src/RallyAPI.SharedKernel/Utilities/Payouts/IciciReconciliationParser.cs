using ClosedXML.Excel;

namespace RallyAPI.SharedKernel.Utilities.Payouts;

/// <summary>
/// One row from ICICI's Consolidated Status Report — the bank's result file for a bulk-transfer
/// batch. Column layout confirmed against a real ICICI report (29 columns: File_Sequence_Num
/// through UTR NO). <see cref="Outcome"/> classifies the row so callers don't need to know the
/// bank's exact status vocabulary.
/// </summary>
public sealed record IciciReconciliationRow(
    int RowNumber,
    string BeneficiaryName,
    string AccountNumber,
    string IfscCode,
    decimal Amount,
    string Status,
    string? CurrentStep,
    string? RejectionReason,
    string? Utr)
{
    /// <summary>Bank confirms the transfer succeeded — safe to mark Paid, given a valid UTR.</summary>
    public bool IsSuccess =>
        string.Equals(Status, "Success", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(Utr);

    /// <summary>Bank rejected or reversed the transfer — safe to mark Failed.</summary>
    public bool IsFailed =>
        !string.IsNullOrWhiteSpace(RejectionReason)
        || Status is "Reversed" or "Rejected" or "Failed" or "Return" or "Returned"
        || (CurrentStep is "Reversed" or "Rejected" or "Failed");

    /// <summary>Still in flight at the bank (e.g. Processing/Pending in ICICI's own pipeline) — leave untouched.</summary>
    public bool IsUnresolved => !IsSuccess && !IsFailed;
}

/// <summary>
/// Parses an ICICI Consolidated Status Report .xlsx into <see cref="IciciReconciliationRow"/>s.
/// Column layout is looked up by header name (row 1), not fixed position, so a reordered or
/// slightly different bank template still parses as long as the header names match.
/// </summary>
public static class IciciReconciliationParser
{
    private static readonly string[] RequiredHeaders =
    {
        "Beneficiary Account No", "Bene_IFSC_Code", "Amount", "STATUS"
    };

    public static IReadOnlyList<IciciReconciliationRow> Parse(Stream fileStream)
    {
        using var workbook = new XLWorkbook(fileStream);
        var sheet = workbook.Worksheets.First();

        var headerRow = sheet.Row(1);
        var lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        if (lastColumn == 0)
            throw new InvalidOperationException("ICICI report has no header row.");

        var columnByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var col = 1; col <= lastColumn; col++)
        {
            var header = headerRow.Cell(col).GetString().Trim();
            if (header.Length > 0 && !columnByHeader.ContainsKey(header))
                columnByHeader[header] = col;
        }

        var missing = RequiredHeaders.Where(h => !columnByHeader.ContainsKey(h)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"ICICI report is missing required column(s): {string.Join(", ", missing)}.");

        int Col(string header) => columnByHeader[header];
        int? OptionalCol(string header) => columnByHeader.TryGetValue(header, out var c) ? c : null;

        var beneficiaryNameCol = OptionalCol("Beneficiary Name");
        var currentStepCol = OptionalCol("Current Step");
        var rejectionReasonCol = OptionalCol("Rejection Reason");
        var utrCol = OptionalCol("UTR NO");

        var rows = new List<IciciReconciliationRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var r = 2; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            if (row.IsEmpty()) continue;

            var accountNumber = row.Cell(Col("Beneficiary Account No")).GetString().Trim();
            var ifsc = row.Cell(Col("Bene_IFSC_Code")).GetString().Trim();
            if (accountNumber.Length == 0 && ifsc.Length == 0) continue; // blank trailing row

            var amountCell = row.Cell(Col("Amount"));
            if (!amountCell.TryGetValue<decimal>(out var amount))
                throw new InvalidOperationException($"Row {r}: could not parse Amount '{amountCell.GetString()}' as a number.");

            var status = row.Cell(Col("STATUS")).GetString().Trim();

            rows.Add(new IciciReconciliationRow(
                RowNumber: r,
                BeneficiaryName: beneficiaryNameCol is { } bnCol ? row.Cell(bnCol).GetString().Trim() : string.Empty,
                AccountNumber: accountNumber,
                IfscCode: ifsc,
                Amount: amount,
                Status: status,
                CurrentStep: currentStepCol is { } csCol ? NullIfEmpty(row.Cell(csCol).GetString()) : null,
                RejectionReason: rejectionReasonCol is { } rrCol ? NullIfEmpty(row.Cell(rrCol).GetString()) : null,
                Utr: utrCol is { } uCol ? NullIfEmpty(row.Cell(uCol).GetString()) : null));
        }

        return rows;
    }

    private static string? NullIfEmpty(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

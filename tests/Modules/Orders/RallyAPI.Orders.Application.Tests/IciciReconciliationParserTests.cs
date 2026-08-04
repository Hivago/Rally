using ClosedXML.Excel;
using FluentAssertions;
using RallyAPI.SharedKernel.Utilities.Payouts;
using Xunit;

namespace RallyAPI.Orders.Application.Tests;

/// <summary>
/// Verifies the parser against the real ICICI Consolidated Status Report column layout
/// (confirmed against an actual bank-issued report — 29 columns, File_Sequence_Num..UTR NO).
/// </summary>
public class IciciReconciliationParserTests
{
    private static readonly string[] FullHeaderRow =
    {
        "File_Sequence_Num", "Pymt_Prod_Type_Code", "Pymt_Mode", "Debit_Acct_no",
        "Beneficiary Name", "Beneficiary Account No", "Bene_IFSC_Code", "Amount",
        "Debit narration", "Credit narration", "Mobile Numder", "Email id", "Remark",
        "Pymt_Date", "Reference_no", "Addl_Info1", "Addl_Info2", "Addl_Info3", "Addl_Info4",
        "Addl_Info5", "Beneficiary LEI", "STATUS", "Current Step", "File name",
        "Rejected by", "Rejection Reason", "Acct_Debit_date", "Customer Ref No", "UTR NO"
    };

    private static byte[] BuildReport(IReadOnlyList<string> headers, IReadOnlyList<object?[]> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");

        for (var c = 0; c < headers.Count; c++)
            sheet.Cell(1, c + 1).Value = headers[c];

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var c = 0; c < row.Length; c++)
            {
                var value = row[c];
                if (value is decimal d) sheet.Cell(r + 2, c + 1).Value = d;
                else sheet.Cell(r + 2, c + 1).Value = value?.ToString() ?? string.Empty;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static object?[] Row(
        string beneficiaryName, string accountNo, string ifsc, decimal amount,
        string status, string currentStep, string rejectionReason, string utr)
    {
        // Matches FullHeaderRow's 29-column order exactly.
        return new object?[]
        {
            "75120448", "PAB_VENDOR", "NEFT", "473705000191",
            beneficiaryName, accountNo, ifsc, amount,
            "", "", "", "", "",
            "16-07-2026", "", "", "", "", "",
            "", "", status, currentStep, "Vendor June Salary 16072026",
            "", rejectionReason, "16-07-2026", "2458851071", utr
        };
    }

    [Fact]
    public void Parse_SuccessRowWithUtr_ClassifiesAsSuccess()
    {
        var bytes = BuildReport(FullHeaderRow, new[]
        {
            Row("NARENDRA SOMAI", "7795632875", "IDIB000V514", 36170.0m, "Success", "Paid", "", "IN42619755781929")
        });

        using var stream = new MemoryStream(bytes);
        var rows = IciciReconciliationParser.Parse(stream);

        rows.Should().HaveCount(1);
        var row = rows[0];
        row.AccountNumber.Should().Be("7795632875");
        row.IfscCode.Should().Be("IDIB000V514");
        row.Amount.Should().Be(36170.0m);
        row.IsSuccess.Should().BeTrue();
        row.IsFailed.Should().BeFalse();
        row.Utr.Should().Be("IN42619755781929");
    }

    [Fact]
    public void Parse_ReversedRow_ClassifiesAsFailed()
    {
        var bytes = BuildReport(FullHeaderRow, new[]
        {
            Row("KULDEEP SINGH RAWAT", "66510100004216", "BARB0VJDAPB", 12000.0m, "Reversed", "Reversed", "", "")
        });

        using var stream = new MemoryStream(bytes);
        var rows = IciciReconciliationParser.Parse(stream);

        rows.Should().HaveCount(1);
        rows[0].IsFailed.Should().BeTrue();
        rows[0].IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Parse_RowWithRejectionReasonButNoStatusMatch_StillClassifiesAsFailed()
    {
        var bytes = BuildReport(FullHeaderRow, new[]
        {
            Row("ABDUL SHAIKH", "102303138000096", "SBIN0002403", 5000.0m, "Rejected", "", "Invalid account number", "")
        });

        using var stream = new MemoryStream(bytes);
        var rows = IciciReconciliationParser.Parse(stream);

        rows[0].IsFailed.Should().BeTrue();
        rows[0].RejectionReason.Should().Be("Invalid account number");
    }

    [Fact]
    public void Parse_InFlightStatus_IsUnresolved()
    {
        var bytes = BuildReport(FullHeaderRow, new[]
        {
            Row("ABHISEK RAWAT", "44554506938", "SBIN0002403", 27000.0m, "Processing", "In Progress", "", "")
        });

        using var stream = new MemoryStream(bytes);
        var rows = IciciReconciliationParser.Parse(stream);

        rows[0].IsUnresolved.Should().BeTrue();
        rows[0].IsSuccess.Should().BeFalse();
        rows[0].IsFailed.Should().BeFalse();
    }

    [Fact]
    public void Parse_MissingRequiredColumn_Throws()
    {
        var headersWithoutStatus = FullHeaderRow.Where(h => h != "STATUS").ToList();
        var bytes = BuildReport(headersWithoutStatus, new List<object?[]>());

        using var stream = new MemoryStream(bytes);
        var act = () => IciciReconciliationParser.Parse(stream);

        act.Should().Throw<InvalidOperationException>().WithMessage("*STATUS*");
    }

    [Fact]
    public void Parse_HeaderNameLookupIsCaseInsensitive()
    {
        var lowerHeaders = FullHeaderRow.Select(h => h.ToLowerInvariant()).ToList();
        var bytes = BuildReport(lowerHeaders, new[]
        {
            Row("Test Bene", "111222333", "ICIC0001234", 1000.0m, "Success", "Paid", "", "IN42619755781999")
        });

        using var stream = new MemoryStream(bytes);
        var rows = IciciReconciliationParser.Parse(stream);

        rows.Should().HaveCount(1);
        rows[0].IsSuccess.Should().BeTrue();
    }
}

namespace RallyAPI.Orders.Domain.Enums;

public enum PayoutExportBatchStatus
{
    /// <summary>File generated and downloaded, awaiting the ICICI bank statement upload.</summary>
    Generated = 0,

    /// <summary>Every row in this batch has been resolved (Paid or Failed) via bank statement reconciliation.</summary>
    Reconciled = 1
}

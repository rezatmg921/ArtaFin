[CmdletBinding()]
param(
    [string]$BaseUrl = "https://localhost:57796",
    [Parameter(Mandatory = $true)]
    [string]$Token,
    [Guid]$FinancialBookId = "f098d1f6-4377-4dc5-8a1d-39e0ea721d70",
    [DateTimeOffset]$From = [DateTimeOffset]::Parse("2025-12-31T20:30:00+00:00"),
    [DateTimeOffset]$To = [DateTimeOffset]::Parse("2026-11-27T20:30:00+00:00"),
    [switch]$SkipCertificateCheck
)

$ErrorActionPreference = "Stop"

if ($From -ge $To) {
    throw "From must be earlier than To."
}

$headers = @{
    Authorization = "Bearer $Token"
    Accept = "application/json"
}

function Invoke-ArtaFinGet {
    param([Parameter(Mandatory = $true)][string]$Uri)

    if ($SkipCertificateCheck) {
        if ($PSVersionTable.PSVersion.Major -lt 7) {
            throw "SkipCertificateCheck requires PowerShell 7+. Trust the ASP.NET development certificate or rerun without this switch."
        }

        return Invoke-RestMethod -Method Get -Uri $Uri -Headers $headers -SkipCertificateCheck
    }

    return Invoke-RestMethod -Method Get -Uri $Uri -Headers $headers
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "ASSERT FAILED: $Message" }
}

function Assert-Amount {
    param([decimal]$Actual, [decimal]$Expected, [string]$Message)
    if ([Math]::Abs($Actual - $Expected) -gt 0.0001) {
        throw "ASSERT FAILED: $Message. Expected=$Expected Actual=$Actual"
    }
}

$book = $FinancialBookId.ToString()
$fromText = [Uri]::EscapeDataString($From.ToString("o"))
$toText = [Uri]::EscapeDataString($To.ToString("o"))
$root = "$($BaseUrl.TrimEnd('/'))/api/financial-books/$book/reports"

Write-Host "Calling Cash Flow Statement..." -ForegroundColor Cyan
$cashFlow = Invoke-ArtaFinGet -Uri "$root/cash-flow-statement?from=$fromText&to=$toText"

Write-Host "Calling Cash Flow Reconciliation..." -ForegroundColor Cyan
$reconciliation = Invoke-ArtaFinGet -Uri "$root/cash-flow-reconciliation?from=$fromText&to=$toText"

$cashFlowReceipts = [decimal](($cashFlow.activities | Measure-Object -Property cashReceipts -Sum).Sum)
$cashFlowPayments = [decimal](($cashFlow.activities | Measure-Object -Property cashPayments -Sum).Sum)
$internalTransfers = [decimal](($cashFlow.internalCashTransfers | Measure-Object -Property amount -Sum).Sum)
$accountDebits = [decimal](($reconciliation.accounts | Measure-Object -Property debits -Sum).Sum)
$accountCredits = [decimal](($reconciliation.accounts | Measure-Object -Property credits -Sum).Sum)

Assert-True ([bool]$reconciliation.isReconciled) "Reconciliation endpoint must return isReconciled=true."
Assert-True ([bool]$cashFlow.isReconciled) "Cash Flow Statement must return isReconciled=true."
Assert-True ([DateTimeOffset]::Parse($reconciliation.from) -eq $From) "Reconciliation From must match the requested From."
Assert-True ([DateTimeOffset]::Parse($reconciliation.to) -eq $To) "Reconciliation To must match the requested To."
Assert-True ([DateTimeOffset]::Parse($reconciliation.trialBalanceAsOf) -eq $To) "TrialBalanceAsOf must expose the logical exclusive To cutoff."

Assert-Amount ([decimal]$reconciliation.cashAccountDebits) $accountDebits "Cash account debit total must equal the sum of account details."
Assert-Amount ([decimal]$reconciliation.cashAccountCredits) $accountCredits "Cash account credit total must equal the sum of account details."
Assert-Amount ([decimal]$reconciliation.internalTransfers) $internalTransfers "Internal transfer total must match the Cash Flow Statement."
Assert-Amount ([decimal]$reconciliation.cashFlowReceipts) $cashFlowReceipts "Receipts must match the Cash Flow Statement."
Assert-Amount ([decimal]$reconciliation.cashFlowPayments) $cashFlowPayments "Payments must match the Cash Flow Statement."
Assert-Amount ([decimal]$reconciliation.externalCashInflowsFromTrialBalance) ($accountDebits - $internalTransfers) "External inflows must exclude internal transfers."
Assert-Amount ([decimal]$reconciliation.externalCashOutflowsFromTrialBalance) ($accountCredits - $internalTransfers) "External outflows must exclude internal transfers."
Assert-Amount ([decimal]$reconciliation.receiptsDifference) 0 "Receipts difference must be zero."
Assert-Amount ([decimal]$reconciliation.paymentsDifference) 0 "Payments difference must be zero."

Write-Host ""
Write-Host "PASS: Cash Flow Reconciliation integration checks succeeded." -ForegroundColor Green
[PSCustomObject]@{
    FinancialBookId = $FinancialBookId
    From = $reconciliation.from
    To = $reconciliation.to
    Currency = $reconciliation.currency
    CashAccountDebits = $reconciliation.cashAccountDebits
    CashAccountCredits = $reconciliation.cashAccountCredits
    InternalTransfers = $reconciliation.internalTransfers
    ExternalCashInflows = $reconciliation.externalCashInflowsFromTrialBalance
    ExternalCashOutflows = $reconciliation.externalCashOutflowsFromTrialBalance
    CashFlowReceipts = $reconciliation.cashFlowReceipts
    CashFlowPayments = $reconciliation.cashFlowPayments
    ReceiptsDifference = $reconciliation.receiptsDifference
    PaymentsDifference = $reconciliation.paymentsDifference
    IsReconciled = $reconciliation.isReconciled
    CashAccounts = $reconciliation.accounts.Count
} | Format-List

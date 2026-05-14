using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace JsonIngestService.DataAccess;

/// <summary>
/// Thin wrapper around SqlBulkCopy.
/// Each call maps DataTable columns to destination columns by name,
/// so column order in the DataTable does not matter.
/// </summary>
public sealed class BulkCopyHelper
{
    private readonly int _batchSize;
    private readonly int _timeoutSeconds;
    private readonly ILogger<BulkCopyHelper> _logger;

    public BulkCopyHelper(int batchSize, int timeoutSeconds, ILogger<BulkCopyHelper> logger)
    {
        _batchSize      = batchSize;
        _timeoutSeconds = timeoutSeconds;
        _logger         = logger;
    }

    /// <summary>
    /// Bulk-inserts all rows in <paramref name="dataTable"/> into
    /// <paramref name="destinationTable"/> within the supplied transaction.
    /// No-ops when the DataTable is empty.
    /// </summary>
    public async Task InsertAsync(
        string destinationTable,
        DataTable dataTable,
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (dataTable.Rows.Count == 0)
            return;

        _logger.LogDebug(
            "BulkCopy → {Table}: {Rows} rows",
            destinationTable, dataTable.Rows.Count);

        // CheckConstraints validates CHECK constraints per batch.
        // TableLock is intentionally omitted — multiple pods may insert
        // concurrently to the same table for different files.
        using var bulk = new SqlBulkCopy(
            connection,
            SqlBulkCopyOptions.CheckConstraints,
            transaction)
        {
            DestinationTableName = destinationTable,
            BatchSize            = _batchSize,
            BulkCopyTimeout      = _timeoutSeconds,
            EnableStreaming       = true
        };

        foreach (DataColumn col in dataTable.Columns)
            bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);

        await bulk.WriteToServerAsync(dataTable, cancellationToken);
    }
}

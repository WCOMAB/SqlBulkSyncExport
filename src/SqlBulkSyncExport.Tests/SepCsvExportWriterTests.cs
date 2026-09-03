using System.Collections;
using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging.Abstractions;
using SqlBulkSyncExport.Services.Csv;

namespace SqlBulkSyncExport.Tests;

public sealed class SepCsvExportWriterTests
{
    private static readonly TimeZoneInfo PlusTwo =
        TimeZoneInfo.CreateCustomTimeZone(
            "Test/PlusTwo",
            TimeSpan.FromHours(2),
            "Test Plus Two",
            "Test Plus Two");

    [Fact]
    public async Task WriteAsync_WritesHeaderSeparatorNullBinaryAndCrlf()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Payload", typeof(byte[]));
        table.Rows.Add(1, "Alice", new byte[] { 1, 2, 3 });
        table.Rows.Add(2, DBNull.Value, Array.Empty<byte>());

        var path = Path.Combine(Path.GetTempPath(), $"sbs-csv-{Guid.NewGuid():N}.csv");
        try
        {
            await using var reader = table.CreateDataReader();
            var writer = new SepCsvExportWriter(NullLogger<SepCsvExportWriter>.Instance);
            var rows = await writer.WriteAsync(
                path,
                reader,
                new CsvWriteOptions(',', IncludeHeader: true, NewLine: "\r\n", SourceTimeZone: TimeZoneInfo.Utc, ProgressLogBatchSize: 0),
                TestContext.Current.CancellationToken);

            Assert.Equal(2, rows);
            var text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            Assert.Contains("Id,Name,Payload\r\n", text);
            Assert.Contains("1,Alice,AQID\r\n", text);
            Assert.Contains("2,,\r\n", text);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task WriteAsync_FormatsTemporalTypesAsIso()
    {
        var utc = new DateTime(2026, 8, 31, 12, 34, 56, 789, DateTimeKind.Utc);
        var unspecified = new DateTime(2026, 8, 31, 11, 46, 20, 947, DateTimeKind.Unspecified);
        var offset = new DateTimeOffset(2026, 8, 31, 12, 34, 56, 789, TimeSpan.FromHours(2));
        var dateOnly = new DateOnly(2026, 8, 31);
        var timeOnly = new TimeOnly(12, 34, 56, 789);
        var timeSpan = new TimeSpan(0, 12, 34, 56, 789);

        await using var reader = new ObjectRowDbDataReader(
            ["DtUtc", "DtUnspec", "Dto", "D", "T", "Ts"],
            [utc, unspecified, offset, dateOnly, timeOnly, timeSpan]);

        var path = Path.Combine(Path.GetTempPath(), $"sbs-csv-{Guid.NewGuid():N}.csv");
        try
        {
            var writer = new SepCsvExportWriter(NullLogger<SepCsvExportWriter>.Instance);
            await writer.WriteAsync(
                path,
                reader,
                new CsvWriteOptions(',', IncludeHeader: true, NewLine: "\r\n", SourceTimeZone: PlusTwo, ProgressLogBatchSize: 0),
                TestContext.Current.CancellationToken);

            var text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(
                "DtUtc,DtUnspec,Dto,D,T,Ts\r\n" +
                "2026-08-31T12:34:56.789Z,2026-08-31T11:46:20.947+02:00,2026-08-31T12:34:56.789+02:00,2026-08-31,12:34:56.789,12:34:56.789\r\n",
                text);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task WriteAsync_FormatsOutOfRangeDateTimeWithoutOffset()
    {
        // 0001-01-01 +02:00 would make UTC year 0 and throw from DateTimeOffset.
        var minUnspecified = new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        await using var reader = new ObjectRowDbDataReader(
            ["ReturnDate"],
            [minUnspecified]);

        var path = Path.Combine(Path.GetTempPath(), $"sbs-csv-{Guid.NewGuid():N}.csv");
        try
        {
            var writer = new SepCsvExportWriter(NullLogger<SepCsvExportWriter>.Instance);
            await writer.WriteAsync(
                path,
                reader,
                new CsvWriteOptions(',', IncludeHeader: true, NewLine: "\r\n", SourceTimeZone: PlusTwo, ProgressLogBatchSize: 0),
                TestContext.Current.CancellationToken);

            var text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(
                "ReturnDate\r\n" +
                "0001-01-01T00:00:00.000\r\n",
                text);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task WriteAsync_CanDisableHeader()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Rows.Add(7);

        var path = Path.Combine(Path.GetTempPath(), $"sbs-csv-{Guid.NewGuid():N}.csv");
        try
        {
            await using var reader = table.CreateDataReader();
            var writer = new SepCsvExportWriter(NullLogger<SepCsvExportWriter>.Instance);
            await writer.WriteAsync(
                path,
                reader,
                new CsvWriteOptions(';', IncludeHeader: false, NewLine: "\n", SourceTimeZone: TimeZoneInfo.Utc, ProgressLogBatchSize: 0),
                TestContext.Current.CancellationToken);

            var text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            Assert.DoesNotContain("Id", text);
            Assert.Equal("7\n", text);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task WriteAsync_WhenCellThrows_OuterExceptionNamesColumn()
    {
        var boom = new InvalidOperationException("boom-cell");
        await using var reader = new ObjectRowDbDataReader(
            ["Id", "Boom", "After"],
            [1, "trigger", "tail"],
            throwOnGetValue: ordinal => ordinal == 1 ? boom : null);

        var path = Path.Combine(Path.GetTempPath(), $"sbs-csv-{Guid.NewGuid():N}.csv");
        try
        {
            var writer = new SepCsvExportWriter(NullLogger<SepCsvExportWriter>.Instance);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync(
                path,
                reader,
                new CsvWriteOptions(',', IncludeHeader: true, NewLine: "\r\n", SourceTimeZone: TimeZoneInfo.Utc, ProgressLogBatchSize: 0),
                TestContext.Current.CancellationToken));

            Assert.Contains("Boom", ex.Message, StringComparison.Ordinal);
            Assert.Contains("ordinal 1", ex.Message, StringComparison.Ordinal);
            Assert.Contains("data row 1", ex.Message, StringComparison.Ordinal);
            Assert.Contains(path, ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("Not all expected columns", ex.Message, StringComparison.Ordinal);
            Assert.Same(boom, ex.InnerException);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>Minimal reader that preserves boxed value types (including DateTimeKind).</summary>
    private sealed class ObjectRowDbDataReader(
        string[] names,
        object[] values,
        Func<int, Exception?>? throwOnGetValue = null) : DbDataReader
    {
        private bool _read;

        public override int FieldCount => names.Length;

        public override bool HasRows => true;

        public override bool IsClosed => false;

        public override int RecordsAffected => 0;

        public override int Depth => 0;

        public override object this[int ordinal] => GetValue(ordinal);

        public override object this[string name] => GetValue(GetOrdinal(name));

        public override bool Read()
        {
            if (_read)
            {
                return false;
            }

            _read = true;
            return true;
        }

        public override string GetName(int ordinal) => names[ordinal];

        public override int GetOrdinal(string name) => Array.IndexOf(names, name);

        public override object GetValue(int ordinal)
        {
            var failure = throwOnGetValue?.Invoke(ordinal);
            if (failure is not null)
            {
                throw failure;
            }

            return values[ordinal];
        }

        public override bool IsDBNull(int ordinal) => values[ordinal] is DBNull or null;

        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());

        public override bool NextResult() => false;

        public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);

        public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
            => throw new NotSupportedException();

        public override char GetChar(int ordinal) => (char)GetValue(ordinal);

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
            => throw new NotSupportedException();

        public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

        public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);

        public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);

        public override double GetDouble(int ordinal) => (double)GetValue(ordinal);

        public override Type GetFieldType(int ordinal)
            => values[ordinal] is DBNull or null ? typeof(object) : values[ordinal].GetType();

        public override float GetFloat(int ordinal) => (float)GetValue(ordinal);

        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);

        public override short GetInt16(int ordinal) => (short)GetValue(ordinal);

        public override int GetInt32(int ordinal) => (int)GetValue(ordinal);

        public override long GetInt64(int ordinal) => (long)GetValue(ordinal);

        public override string GetString(int ordinal) => (string)GetValue(ordinal);

        public override int GetValues(object[] valuesBuffer)
        {
            Array.Copy(values, valuesBuffer, values.Length);
            return values.Length;
        }

        public override IEnumerator GetEnumerator() => values.GetEnumerator();
    }
}

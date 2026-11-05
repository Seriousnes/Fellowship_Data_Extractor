using Microsoft.Data.Sqlite;

namespace Fellowship.Codex.Usage;

public sealed record AreaUsage(UsageArea Area, long Requests, long ClientErrors, long ServerErrors, long Cached);

public sealed record RouteUsage(
    UsageArea Area,
    string Method,
    string Route,
    string Entity,
    long Requests,
    long ClientErrors,
    long ServerErrors,
    long Cached,
    double MeanMilliseconds,
    double MaxMilliseconds,
    int? P95Bucket);

public sealed record StatusUsage(int Status, long Requests);

/// <summary>Requests of one browser family on one system, by area.</summary>
public sealed record ClientUsage(
    string Browser,
    string Os,
    bool Mobile,
    long Pages,
    long Circuits,
    long Cards,
    long Api,
    long Static,
    long Requests,
    long ClientErrors,
    long ServerErrors)
{
    public ClientKind Kind => UserAgents.Kind(Browser);
}

/// <summary>Requests and errors of one browser version in one area, for a version with an error.</summary>
public sealed record VersionErrors(
    UsageArea Area, string Browser, int Major, string Os, long Requests, long ClientErrors, long ServerErrors);

public sealed record DayUsage(long Day, UsageArea Area, long Requests);

public sealed record SeriesPoint(long Bucket, string Series, long Requests);

public sealed record ErrorUsage(string Category, string Exception, long Count);

/// <summary>The SQLite database of usage counts.</summary>
public sealed class UsageStore
{
    private static readonly string Buckets =
        string.Join(", ", Enumerable.Range(0, Latency.Count).Select(bucket => $"b{bucket}"));

    private readonly string connection;

    public UsageStore(string path)
    {
        Path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);

        connection = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        using var open = Open();
        Execute(open, null, Schema);
    }

    public string Path { get; }

    private static string Schema =>
        $"""
        PRAGMA journal_mode = WAL;
        CREATE TABLE IF NOT EXISTS requests (
            hour INTEGER NOT NULL,
            area TEXT NOT NULL,
            method TEXT NOT NULL,
            route TEXT NOT NULL,
            entity TEXT NOT NULL,
            status INTEGER NOT NULL,
            cached INTEGER NOT NULL,
            count INTEGER NOT NULL,
            sum_ms REAL NOT NULL,
            max_ms REAL NOT NULL,
            {string.Join(",\n    ", Enumerable.Range(0, Latency.Count).Select(bucket => $"b{bucket} INTEGER NOT NULL"))},
            PRIMARY KEY (hour, area, method, route, entity, status, cached)
        ) WITHOUT ROWID;
        CREATE TABLE IF NOT EXISTS agents (
            day INTEGER NOT NULL,
            browser TEXT NOT NULL,
            major INTEGER NOT NULL,
            os TEXT NOT NULL,
            mobile INTEGER NOT NULL,
            area TEXT NOT NULL,
            status_class INTEGER NOT NULL,
            count INTEGER NOT NULL,
            PRIMARY KEY (day, browser, major, os, mobile, area, status_class)
        ) WITHOUT ROWID;
        CREATE TABLE IF NOT EXISTS errors (
            hour INTEGER NOT NULL,
            category TEXT NOT NULL,
            exception TEXT NOT NULL,
            count INTEGER NOT NULL,
            PRIMARY KEY (hour, category, exception)
        ) WITHOUT ROWID;
        CREATE TABLE IF NOT EXISTS minutes (
            minute INTEGER NOT NULL,
            kind TEXT NOT NULL,
            area TEXT NOT NULL,
            count INTEGER NOT NULL,
            PRIMARY KEY (minute, kind, area)
        ) WITHOUT ROWID;
        """;

    private SqliteConnection Open()
    {
        var open = new SqliteConnection(connection);
        open.Open();
        Execute(open, null, "PRAGMA busy_timeout = 5000;");

        return open;
    }

    private static void Execute(SqliteConnection open, SqliteTransaction? transaction, string sql)
    {
        using var command = open.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Write(UsageBatch batch)
    {
        if (batch.Empty) return;

        using var open = Open();
        using var transaction = open.BeginTransaction();

        WriteRequests(open, transaction, batch.Requests);
        WriteAgents(open, transaction, batch.Agents);
        WriteErrors(open, transaction, batch.Errors);
        WriteMinutes(open, transaction, batch.Minutes);

        transaction.Commit();
    }

    private static void WriteMinutes(
        SqliteConnection open, SqliteTransaction transaction, IReadOnlyDictionary<MinuteKey, long> minutes)
    {
        using var command = open.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO minutes (minute, kind, area, count) VALUES ($minute, $kind, $area, $count)
            ON CONFLICT DO UPDATE SET count = count + excluded.count;
            """;

        var minute = command.Parameters.Add("$minute", SqliteType.Integer);
        var kind = command.Parameters.Add("$kind", SqliteType.Text);
        var area = command.Parameters.Add("$area", SqliteType.Text);
        var count = command.Parameters.Add("$count", SqliteType.Integer);

        foreach (var (key, total) in minutes)
        {
            minute.Value = key.Minute;
            kind.Value = key.Kind.ToString();
            area.Value = key.Area.ToString();
            count.Value = total;
            command.ExecuteNonQuery();
        }
    }

    private static void WriteRequests(
        SqliteConnection open, SqliteTransaction transaction, IReadOnlyDictionary<RequestKey, RequestTally> requests)
    {
        using var command = open.CreateCommand();
        command.Transaction = transaction;

        var bucketValues = string.Join(", ", Enumerable.Range(0, Latency.Count).Select(bucket => $"$b{bucket}"));
        var bucketSums = string.Join(", ", Enumerable.Range(0, Latency.Count).Select(bucket => $"b{bucket} = b{bucket} + excluded.b{bucket}"));

        command.CommandText =
            $"""
            INSERT INTO requests (hour, area, method, route, entity, status, cached, count, sum_ms, max_ms, {Buckets})
            VALUES ($hour, $area, $method, $route, $entity, $status, $cached, $count, $sum, $max, {bucketValues})
            ON CONFLICT DO UPDATE SET
                count = count + excluded.count,
                sum_ms = sum_ms + excluded.sum_ms,
                max_ms = max(max_ms, excluded.max_ms),
                {bucketSums};
            """;

        string[] names = ["$hour", "$area", "$method", "$route", "$entity", "$status", "$cached", "$count", "$sum", "$max"];
        var parameters = names.Concat(Enumerable.Range(0, Latency.Count).Select(bucket => $"$b{bucket}"))
            .Select(name => command.Parameters.Add(new SqliteParameter { ParameterName = name }))
            .ToArray();

        foreach (var (key, tally) in requests)
        {
            object[] values =
            [
                key.Hour, key.Area.ToString(), key.Method, key.Route, key.Entity, key.Status, key.Cached ? 1 : 0,
                tally.Count, tally.SumMilliseconds, tally.MaxMilliseconds,
            ];

            for (var at = 0; at < values.Length; at++) parameters[at].Value = values[at];
            for (var bucket = 0; bucket < Latency.Count; bucket++) parameters[values.Length + bucket].Value = tally.Buckets[bucket];

            command.ExecuteNonQuery();
        }
    }

    private static void WriteAgents(
        SqliteConnection open, SqliteTransaction transaction, IReadOnlyDictionary<AgentKey, long> agents)
    {
        using var command = open.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO agents (day, browser, major, os, mobile, area, status_class, count)
            VALUES ($day, $browser, $major, $os, $mobile, $area, $class, $count)
            ON CONFLICT DO UPDATE SET count = count + excluded.count;
            """;

        var day = command.Parameters.Add("$day", SqliteType.Integer);
        var browser = command.Parameters.Add("$browser", SqliteType.Text);
        var major = command.Parameters.Add("$major", SqliteType.Integer);
        var os = command.Parameters.Add("$os", SqliteType.Text);
        var mobile = command.Parameters.Add("$mobile", SqliteType.Integer);
        var area = command.Parameters.Add("$area", SqliteType.Text);
        var statusClass = command.Parameters.Add("$class", SqliteType.Integer);
        var count = command.Parameters.Add("$count", SqliteType.Integer);

        foreach (var (key, total) in agents)
        {
            day.Value = key.Day;
            browser.Value = key.Agent.Browser;
            major.Value = key.Agent.Major;
            os.Value = key.Agent.Os;
            mobile.Value = key.Agent.Mobile ? 1 : 0;
            area.Value = key.Area.ToString();
            statusClass.Value = key.StatusClass;
            count.Value = total;
            command.ExecuteNonQuery();
        }
    }

    private static void WriteErrors(
        SqliteConnection open, SqliteTransaction transaction, IReadOnlyDictionary<ErrorKey, long> errors)
    {
        using var command = open.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO errors (hour, category, exception, count) VALUES ($hour, $category, $exception, $count)
            ON CONFLICT DO UPDATE SET count = count + excluded.count;
            """;

        var hour = command.Parameters.Add("$hour", SqliteType.Integer);
        var category = command.Parameters.Add("$category", SqliteType.Text);
        var exception = command.Parameters.Add("$exception", SqliteType.Text);
        var count = command.Parameters.Add("$count", SqliteType.Integer);

        foreach (var (key, total) in errors)
        {
            hour.Value = key.Hour;
            category.Value = key.Category;
            exception.Value = key.Exception;
            count.Value = total;
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Deletes every count older than the cutoff, and merges each hourly count older than the rollup
    /// into one count for its UTC day, so the database grows by day past the rollup.
    /// </summary>
    public void Prune(DateTimeOffset cutoff, DateTimeOffset rollup, DateTimeOffset minutes)
    {
        using var open = Open();
        using var transaction = open.BeginTransaction();
        using var command = open.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            DELETE FROM requests WHERE hour < $cutoff;
            DELETE FROM agents WHERE day < $cutoff;
            DELETE FROM errors WHERE hour < $cutoff;
            DELETE FROM minutes WHERE minute < $minutes;

            CREATE TEMP TABLE rolled_requests AS
                SELECT hour / 86400 * 86400, area, method, route, entity, status, cached,
                       sum(count), sum(sum_ms), max(max_ms),
                       {string.Join(", ", Enumerable.Range(0, Latency.Count).Select(bucket => $"sum(b{bucket})"))}
                FROM requests WHERE hour < $rollup GROUP BY 1, 2, 3, 4, 5, 6, 7;
            DELETE FROM requests WHERE hour < $rollup;
            INSERT INTO requests SELECT * FROM rolled_requests;
            DROP TABLE rolled_requests;

            CREATE TEMP TABLE rolled_errors AS
                SELECT hour / 86400 * 86400, category, exception, sum(count)
                FROM errors WHERE hour < $rollup GROUP BY 1, 2, 3;
            DELETE FROM errors WHERE hour < $rollup;
            INSERT INTO errors SELECT * FROM rolled_errors;
            DROP TABLE rolled_errors;
            """;
        command.Parameters.AddWithValue("$cutoff", cutoff.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$rollup", rollup.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$minutes", minutes.ToUnixTimeSeconds());
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    /// <summary>Deletes every count older than the cutoff and returns the freed pages to the disk.</summary>
    public void Clear(DateTimeOffset cutoff)
    {
        using var open = Open();
        using var transaction = open.BeginTransaction();
        using var command = open.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            DELETE FROM requests WHERE hour < $cutoff;
            DELETE FROM agents WHERE day < $cutoff;
            DELETE FROM errors WHERE hour < $cutoff;
            DELETE FROM minutes WHERE minute < $cutoff;
            """;
        command.Parameters.AddWithValue("$cutoff", cutoff.ToUnixTimeSeconds());
        command.ExecuteNonQuery();
        transaction.Commit();

        using var vacuum = open.CreateCommand();
        vacuum.CommandText = "VACUUM;";
        vacuum.ExecuteNonQuery();
    }

    /// <summary>A consistent copy of the database, written to the given file.</summary>
    public void Snapshot(string file)
    {
        File.Delete(file);

        using var open = Open();
        using var command = open.CreateCommand();
        command.CommandText = "VACUUM INTO $file;";
        command.Parameters.AddWithValue("$file", file);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<AreaUsage> Areas(DateTimeOffset since) =>
        Read(
            """
            SELECT area, sum(count),
                   sum(CASE WHEN status BETWEEN 400 AND 499 THEN count ELSE 0 END),
                   sum(CASE WHEN status >= 500 THEN count ELSE 0 END),
                   sum(CASE WHEN cached = 1 THEN count ELSE 0 END)
            FROM requests WHERE hour >= $since GROUP BY area ORDER BY sum(count) DESC;
            """,
            since,
            row => new AreaUsage(Enum.Parse<UsageArea>(row.GetString(0)), row.GetInt64(1), row.GetInt64(2), row.GetInt64(3), row.GetInt64(4)));

    public IReadOnlyList<RouteUsage> Routes(DateTimeOffset since, int limit) =>
        Read(
            $"""
            SELECT area, method, route, entity, sum(count),
                   sum(CASE WHEN status BETWEEN 400 AND 499 THEN count ELSE 0 END),
                   sum(CASE WHEN status >= 500 THEN count ELSE 0 END),
                   sum(CASE WHEN cached = 1 THEN count ELSE 0 END),
                   sum(sum_ms), max(max_ms),
                   {string.Join(", ", Enumerable.Range(0, Latency.Count).Select(bucket => $"sum(b{bucket})"))}
            FROM requests WHERE hour >= $since
            GROUP BY area, method, route, entity ORDER BY sum(count) DESC LIMIT {limit};
            """,
            since,
            row =>
            {
                var buckets = Enumerable.Range(0, Latency.Count).Select(bucket => row.GetInt64(10 + bucket)).ToArray();
                var timed = buckets.Sum();

                return new RouteUsage(
                    Enum.Parse<UsageArea>(row.GetString(0)), row.GetString(1), row.GetString(2), row.GetString(3),
                    row.GetInt64(4), row.GetInt64(5), row.GetInt64(6), row.GetInt64(7),
                    timed == 0 ? 0 : row.GetDouble(8) / timed, row.GetDouble(9),
                    Latency.Percentile(buckets, 0.95));
            });

    public IReadOnlyList<StatusUsage> Statuses(DateTimeOffset since) =>
        Read(
            "SELECT status, sum(count) FROM requests WHERE hour >= $since GROUP BY status ORDER BY status;",
            since,
            row => new StatusUsage(row.GetInt32(0), row.GetInt64(1)));

    public IReadOnlyList<ClientUsage> Clients(DateTimeOffset since, int limit) =>
        Read(
            $"""
            SELECT browser, os, mobile,
                   {Area(UsageArea.Page)}, {Area(UsageArea.Circuit)}, {Area(UsageArea.Card)},
                   {Area(UsageArea.Api)}, {Area(UsageArea.Static)}, sum(count),
                   sum(CASE WHEN status_class = 4 THEN count ELSE 0 END),
                   sum(CASE WHEN status_class = 5 THEN count ELSE 0 END)
            FROM agents WHERE day >= $since
            GROUP BY browser, os, mobile ORDER BY sum(count) DESC LIMIT {limit};
            """,
            UsageCounter.Day(since),
            row => new ClientUsage(
                row.GetString(0), row.GetString(1), row.GetInt32(2) == 1,
                row.GetInt64(3), row.GetInt64(4), row.GetInt64(5), row.GetInt64(6), row.GetInt64(7),
                row.GetInt64(8), row.GetInt64(9), row.GetInt64(10)));

    public IReadOnlyList<VersionErrors> Versions(DateTimeOffset since) =>
        Read(
            """
            SELECT area, browser, major, os, sum(count),
                   sum(CASE WHEN status_class = 4 THEN count ELSE 0 END),
                   sum(CASE WHEN status_class = 5 THEN count ELSE 0 END)
            FROM agents WHERE day >= $since
            GROUP BY area, browser, major, os HAVING sum(CASE WHEN status_class >= 4 THEN count ELSE 0 END) > 0
            ORDER BY 7 DESC, 6 DESC;
            """,
            UsageCounter.Day(since),
            row => new VersionErrors(
                Enum.Parse<UsageArea>(row.GetString(0)), row.GetString(1), row.GetInt32(2), row.GetString(3),
                row.GetInt64(4), row.GetInt64(5), row.GetInt64(6)));

    /// <summary>Requests per minute since the given time, by client kind.</summary>
    public IReadOnlyList<SeriesPoint> Minutes(DateTimeOffset since) =>
        Read(
            "SELECT minute, kind, sum(count) FROM minutes WHERE minute >= $since GROUP BY 1, 2 ORDER BY 1;",
            since,
            row => new SeriesPoint(row.GetInt64(0), row.GetString(1), row.GetInt64(2)));

    /// <summary>Requests per bucket of the given length since the given time, by area.</summary>
    public IReadOnlyList<SeriesPoint> Series(DateTimeOffset since, long bucketSeconds) =>
        Read(
            $"SELECT hour / {bucketSeconds} * {bucketSeconds}, area, sum(count) FROM requests WHERE hour >= $since GROUP BY 1, 2 ORDER BY 1;",
            since,
            row => new SeriesPoint(row.GetInt64(0), row.GetString(1), row.GetInt64(2)));

    private static string Area(UsageArea area) => $"sum(CASE WHEN area = '{area}' THEN count ELSE 0 END)";

    public IReadOnlyList<DayUsage> Days(DateTimeOffset since) =>
        Read(
            "SELECT hour / 86400 * 86400, area, sum(count) FROM requests WHERE hour >= $since GROUP BY 1, 2 ORDER BY 1;",
            since,
            row => new DayUsage(row.GetInt64(0), Enum.Parse<UsageArea>(row.GetString(1)), row.GetInt64(2)));

    public IReadOnlyList<ErrorUsage> Errors(DateTimeOffset since) =>
        Read(
            "SELECT category, exception, sum(count) FROM errors WHERE hour >= $since GROUP BY 1, 2 ORDER BY 3 DESC;",
            since,
            row => new ErrorUsage(row.GetString(0), row.GetString(1), row.GetInt64(2)));

    private IReadOnlyList<T> Read<T>(string sql, DateTimeOffset since, Func<SqliteDataReader, T> project) =>
        Read(sql, since.ToUnixTimeSeconds(), project);

    private IReadOnlyList<T> Read<T>(string sql, long since, Func<SqliteDataReader, T> project)
    {
        using var open = Open();
        using var command = open.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$since", since);

        using var reader = command.ExecuteReader();
        var rows = new List<T>();

        while (reader.Read()) rows.Add(project(reader));

        return rows;
    }
}

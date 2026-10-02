using ClaimTheSquare.ViewModel;
using Dapper;
using Npgsql;

namespace ClaimTheSquare.Data;

// All SQL-en bor her, ikke i Program.cs. Samme oppdeling som labApi
// (Data/, Endpoints/, Models/) — ruter skal ikke vite hvordan en rad ser ut.
public class TextObjectRepository
{
    private readonly string _connectionString;

    public TextObjectRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres mangler. Sett ConnectionStrings__Postgres.");
    }

    private NpgsqlConnection Connect() => new(_connectionString);

    public async Task<IEnumerable<TextObject>> GetAllAsync()
    {
        using var conn = Connect();

        // Aliasene er ikke pynt: kolonnen heter fore_color, propertyen ForeColor.
        // Uten aliaset får Dapper ingen treff og ForeColor blir null.
        const string sql = """
            SELECT "index"    AS "Index",
                   text       AS "Text",
                   back_color AS "BackColor",
                   fore_color AS "ForeColor"
            FROM text_object
            ORDER BY "index"
            """;

        return await conn.QueryAsync<TextObject>(sql);
    }

    public async Task SaveAsync(TextObject textObject)
    {
        using var conn = Connect();

        // Upsert: en rute kan fylles på nytt. Uten ON CONFLICT blir en gjentatt
        // POST et PRIMARY KEY-brudd og API-et svarer 500.
        const string sql = """
            INSERT INTO text_object ("index", text, back_color, fore_color)
            VALUES (@Index, @Text, @BackColor, @ForeColor)
            ON CONFLICT ("index") DO UPDATE
            SET text       = EXCLUDED.text,
                back_color = EXCLUDED.back_color,
                fore_color = EXCLUDED.fore_color
            """;

        await conn.ExecuteAsync(sql, textObject);
    }

    // Skjemaet kan eies av databasen (db/init) eller av appen. I dev lar vi
    // appen lage tabellen hvis den mangler, så en fersk database virker uten
    // at noen må huske rekkefølgen. I prod settes MIGRATE_ON_STARTUP=false.
    public async Task EnsureSchemaAsync()
    {
        using var conn = Connect();

        const string sql = """
            CREATE TABLE IF NOT EXISTS text_object (
                "index"    integer PRIMARY KEY,
                text       text NOT NULL,
                back_color text NOT NULL,
                fore_color text NOT NULL
            );
            """;

        await conn.ExecuteAsync(sql);
    }
}

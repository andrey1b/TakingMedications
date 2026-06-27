using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using TakingMedications.Models;

namespace TakingMedications.Services;

/// <summary>
/// Хранилище на SQLite для standalone-режима (без Python-приложения).
/// Один файл medications.db на профиль. Реализует оба репозиторных интерфейса,
/// чтобы использовать одно соединение и одну транзакцию при сохранении.
/// </summary>
public class SqliteRepository : IStateRepository, IMedicationsRepository
{
    private readonly SqliteConnection _conn;

    public SqliteRepository(string dbPath)
    {
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        Exec("PRAGMA journal_mode=WAL;");
        InitSchema();
    }

    private void InitSchema()
    {
        Exec("""
            CREATE TABLE IF NOT EXISTS app_settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS medication_sections (
                section_key TEXT PRIMARY KEY,
                title       TEXT NOT NULL DEFAULT '',
                sort_order  INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS medications (
                id          TEXT PRIMARY KEY,
                section_key TEXT NOT NULL,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                time        TEXT NOT NULL DEFAULT '',
                time_note   TEXT NOT NULL DEFAULT '',
                name        TEXT NOT NULL DEFAULT '',
                subtitle    TEXT NOT NULL DEFAULT '',
                note        TEXT NOT NULL DEFAULT '',
                doctor      TEXT NOT NULL DEFAULT '',
                course      TEXT NOT NULL DEFAULT '',
                course_type TEXT NOT NULL DEFAULT '',
                pharmacy    TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS intake_marks (
                date          TEXT NOT NULL,
                medication_id TEXT NOT NULL,
                taken         INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (date, medication_id)
            );
            CREATE TABLE IF NOT EXISTS pressure_log (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                systolic  INTEGER NOT NULL,
                diastolic INTEGER NOT NULL,
                pulse     INTEGER,
                sugar     REAL
            );
            CREATE TABLE IF NOT EXISTS purchases (
                id     INTEGER PRIMARY KEY AUTOINCREMENT,
                date   TEXT NOT NULL,
                med_id TEXT NOT NULL,
                amount REAL NOT NULL DEFAULT 0,
                note   TEXT
            );
            CREATE TABLE IF NOT EXISTS notes (
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                created TEXT NOT NULL,
                text    TEXT NOT NULL DEFAULT '',
                color   TEXT
            );
            """);
    }

    // ── IStateRepository ──────────────────────────────────────────────

    AppState IStateRepository.Load()
    {
        var state = new AppState();

        var settingsJson = GetSetting("settings");
        if (settingsJson is not null)
            state.Settings = JsonConvert.DeserializeObject<AppSettings>(settingsJson) ?? new AppSettings();

        using (var cmd = Cmd("SELECT date, medication_id, taken FROM intake_marks"))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
            {
                if (r.GetInt32(2) != 0)
                    state.SetTaken(r.GetString(0), r.GetString(1), true);
            }

        using (var cmd = Cmd("SELECT timestamp,systolic,diastolic,pulse,sugar FROM pressure_log ORDER BY id"))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                state.PressureLog.Add(new PressureEntry
                {
                    Timestamp = r.GetString(0),
                    Systolic  = r.GetInt32(1),
                    Diastolic = r.GetInt32(2),
                    Pulse     = r.IsDBNull(3) ? null : r.GetInt32(3),
                    Sugar     = r.IsDBNull(4) ? null : r.GetDouble(4)
                });

        using (var cmd = Cmd("SELECT date,med_id,amount,note FROM purchases ORDER BY id"))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                state.Purchases.Add(new PurchaseEntry
                {
                    Date   = r.GetString(0),
                    MedId  = r.GetString(1),
                    Amount = (decimal)r.GetDouble(2),
                    Note   = r.IsDBNull(3) ? null : r.GetString(3)
                });

        using (var cmd = Cmd("SELECT created,text,color FROM notes ORDER BY id"))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                state.Notes.Add(new NoteEntry
                {
                    Created = r.GetString(0),
                    Text    = r.GetString(1),
                    Color   = r.IsDBNull(2) ? null : r.GetString(2)
                });

        return state;
    }

    void IStateRepository.Save(AppState state)
    {
        using var tx = _conn.BeginTransaction();
        try
        {
            UpsertSetting("settings", JsonConvert.SerializeObject(state.Settings), tx);

            ExecTx("DELETE FROM intake_marks", tx);
            foreach (var (date, day) in state.Marks)
                foreach (var (medId, taken) in day)
                {
                    using var c = Cmd("INSERT INTO intake_marks(date,medication_id,taken) VALUES($d,$m,$t)", tx);
                    c.Parameters.AddWithValue("$d", date);
                    c.Parameters.AddWithValue("$m", medId);
                    c.Parameters.AddWithValue("$t", taken ? 1 : 0);
                    c.ExecuteNonQuery();
                }

            ExecTx("DELETE FROM pressure_log", tx);
            foreach (var e in state.PressureLog)
            {
                using var c = Cmd("INSERT INTO pressure_log(timestamp,systolic,diastolic,pulse,sugar) VALUES($ts,$sys,$dia,$p,$s)", tx);
                c.Parameters.AddWithValue("$ts",  e.Timestamp);
                c.Parameters.AddWithValue("$sys", e.Systolic);
                c.Parameters.AddWithValue("$dia", e.Diastolic);
                c.Parameters.AddWithValue("$p",   (object?)e.Pulse  ?? DBNull.Value);
                c.Parameters.AddWithValue("$s",   (object?)e.Sugar  ?? DBNull.Value);
                c.ExecuteNonQuery();
            }

            ExecTx("DELETE FROM purchases", tx);
            foreach (var e in state.Purchases)
            {
                using var c = Cmd("INSERT INTO purchases(date,med_id,amount,note) VALUES($d,$m,$a,$n)", tx);
                c.Parameters.AddWithValue("$d", e.Date);
                c.Parameters.AddWithValue("$m", e.MedId);
                c.Parameters.AddWithValue("$a", (double)e.Amount);
                c.Parameters.AddWithValue("$n", (object?)e.Note ?? DBNull.Value);
                c.ExecuteNonQuery();
            }

            ExecTx("DELETE FROM notes", tx);
            foreach (var e in state.Notes)
            {
                using var c = Cmd("INSERT INTO notes(created,text,color) VALUES($c,$t,$col)", tx);
                c.Parameters.AddWithValue("$c",   e.Created);
                c.Parameters.AddWithValue("$t",   e.Text);
                c.Parameters.AddWithValue("$col", (object?)e.Color ?? DBNull.Value);
                c.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ── IMedicationsRepository ────────────────────────────────────────

    List<MedicationSection> IMedicationsRepository.Load()
    {
        var sectionMap = new Dictionary<string, MedicationSection>();

        using (var cmd = Cmd("SELECT section_key,title FROM medication_sections ORDER BY sort_order"))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
            {
                var key = r.GetString(0);
                sectionMap[key] = new MedicationSection { SectionKey = key, Title = r.GetString(1) };
            }

        if (sectionMap.Count == 0)
        {
            var defaults = new List<MedicationSection>(MedicationSection.CreateDefaults());
            ((IMedicationsRepository)this).Save(defaults);
            return defaults;
        }

        using (var cmd = Cmd("""
            SELECT id,section_key,time,time_note,name,subtitle,note,doctor,course,course_type,pharmacy,description
            FROM medications ORDER BY section_key, sort_order
            """))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
            {
                var med = new Medication
                {
                    Id          = r.GetString(0),
                    SectionKey  = r.GetString(1),
                    Time        = r.GetString(2),
                    TimeNote    = r.GetString(3),
                    Name        = r.GetString(4),
                    Subtitle    = r.GetString(5),
                    Note        = r.GetString(6),
                    Doctor      = r.GetString(7),
                    Course      = r.GetString(8),
                    CourseType  = r.GetString(9),
                    Pharmacy    = r.GetString(10),
                    Description = r.GetString(11)
                };
                if (sectionMap.TryGetValue(med.SectionKey!, out var sec))
                    sec.Items.Add(med);
            }

        // Соблюдаем канонический порядок секций
        var result = new List<MedicationSection>();
        foreach (var d in MedicationSection.CreateDefaults())
        {
            result.Add(sectionMap.TryGetValue(d.SectionKey, out var s)
                ? s
                : new MedicationSection { SectionKey = d.SectionKey, Title = d.Title });
        }
        return result;
    }

    void IMedicationsRepository.Save(IEnumerable<MedicationSection> sections)
    {
        using var tx = _conn.BeginTransaction();
        try
        {
            ExecTx("DELETE FROM medications", tx);
            ExecTx("DELETE FROM medication_sections", tx);

            int secOrd = 0;
            foreach (var sec in sections)
            {
                using (var c = Cmd("INSERT INTO medication_sections(section_key,title,sort_order) VALUES($k,$t,$o)", tx))
                {
                    c.Parameters.AddWithValue("$k", sec.SectionKey);
                    c.Parameters.AddWithValue("$t", sec.Title);
                    c.Parameters.AddWithValue("$o", secOrd++);
                    c.ExecuteNonQuery();
                }

                int medOrd = 0;
                foreach (var med in sec.Items)
                {
                    using var c = Cmd("""
                        INSERT INTO medications
                            (id,section_key,sort_order,time,time_note,name,subtitle,note,doctor,course,course_type,pharmacy,description)
                        VALUES($id,$sk,$o,$ti,$tn,$na,$su,$no,$do,$co,$ct,$ph,$de)
                        """, tx);
                    c.Parameters.AddWithValue("$id", med.Id);
                    c.Parameters.AddWithValue("$sk", sec.SectionKey);
                    c.Parameters.AddWithValue("$o",  medOrd++);
                    c.Parameters.AddWithValue("$ti", med.Time);
                    c.Parameters.AddWithValue("$tn", med.TimeNote);
                    c.Parameters.AddWithValue("$na", med.Name);
                    c.Parameters.AddWithValue("$su", med.Subtitle);
                    c.Parameters.AddWithValue("$no", med.Note);
                    c.Parameters.AddWithValue("$do", med.Doctor);
                    c.Parameters.AddWithValue("$co", med.Course);
                    c.Parameters.AddWithValue("$ct", med.CourseType);
                    c.Parameters.AddWithValue("$ph", med.Pharmacy);
                    c.Parameters.AddWithValue("$de", med.Description);
                    c.ExecuteNonQuery();
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ── helpers ───────────────────────────────────────────────────────

    private string? GetSetting(string key)
    {
        using var cmd = Cmd("SELECT value FROM app_settings WHERE key=$k");
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    private void UpsertSetting(string key, string value, SqliteTransaction tx)
    {
        using var c = Cmd("INSERT OR REPLACE INTO app_settings(key,value) VALUES($k,$v)", tx);
        c.Parameters.AddWithValue("$k", key);
        c.Parameters.AddWithValue("$v", value);
        c.ExecuteNonQuery();
    }

    private void Exec(string sql)
    {
        using var c = _conn.CreateCommand();
        c.CommandText = sql;
        c.ExecuteNonQuery();
    }

    private void ExecTx(string sql, SqliteTransaction tx)
    {
        using var c = _conn.CreateCommand();
        c.Transaction = tx;
        c.CommandText = sql;
        c.ExecuteNonQuery();
    }

    private SqliteCommand Cmd(string sql, SqliteTransaction? tx = null)
    {
        var c = _conn.CreateCommand();
        c.CommandText = sql;
        if (tx is not null) c.Transaction = tx;
        return c;
    }
}

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace MultiRept {
    class DB : IDisposable {
        private const int BUFFER_SIZE = 4 * 1024;

        private const string FILEINFO_CREATE = @"
            create table t_fileinfo( 
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                act_no          INTEGER,
                filepath        TEXT,
                guarantee_hash  TEXT
            )";
        private const string FILESTORE_CREATE = @"
            create table t_filestore(
                id              INTEGER PRIMARY KEY,
                bin             BLOB
            )
            ";


        static readonly string DB_TEMP;
        static DB() {
            DB_TEMP = Path.GetTempFileName();

            var sqlConnectionSb = new SQLiteConnectionStringBuilder { DataSource = DB_TEMP };
            using (var cn = new SQLiteConnection(sqlConnectionSb.ToString())) {
                cn.Open();

                using (var cmd = new SQLiteCommand(cn)) {
                    cmd.CommandText = FILEINFO_CREATE;
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = FILESTORE_CREATE;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        SQLiteConnection conn;

        public DB() {
            var sqlConnectionSb = new SQLiteConnectionStringBuilder { DataSource = DB_TEMP };
            conn = new SQLiteConnection(sqlConnectionSb.ToString());
            conn.Open();
        }

        public void Dispose() {
            if (conn != null) {
                conn.Close();
                File.Delete(DB_TEMP);
            }
        }

        public int Insert(ReplacedFile filekey, FileInfo uploadFile) {
            using (var cmd = new SQLiteCommand(conn)) {
                cmd.CommandText = @"insert into t_fileinfo (act_no, filepath, guarantee_hash) values (?,?,?)";
                cmd.Parameters.Add(new SQLiteParameter() { Value = filekey.ActNo });
                cmd.Parameters.Add(new SQLiteParameter() { Value = filekey.FilePath });
                cmd.Parameters.Add(new SQLiteParameter() { Value = filekey.ReplacedFileHash });
                cmd.ExecuteNonQuery();

                int lastInsertId;

                cmd.Parameters.Clear();
                cmd.CommandText = "select last_insert_rowid()";
                using (var reader = cmd.ExecuteReader()) {
                    reader.Read();
                    lastInsertId = reader.GetInt32(0);
                }

                cmd.Parameters.Clear();
                cmd.CommandText = @"insert into t_filestore (id, bin) values (?,zeroblob(?))";
                cmd.Parameters.Add(new SQLiteParameter() { Value = lastInsertId });
                cmd.Parameters.Add(new SQLiteParameter() { Value = uploadFile.Length });
                cmd.ExecuteNonQuery();

                cmd.Parameters.Clear();
                cmd.CommandText = "select id as bin from t_filestore where id = ?";
                cmd.Parameters.Add(new SQLiteParameter() { Value = lastInsertId });
                using (var reader = cmd.ExecuteReader(System.Data.CommandBehavior.KeyInfo)) {
                    reader.Read();

                    using (var blob = reader.GetBlob(0, false)) {
                        using (var stream = uploadFile.OpenRead()) {
                            byte[] buffer = new byte[BUFFER_SIZE];
                            int blobOffest = 0;

                            for (; ; ) {
                                int read = stream.Read(buffer, 0, buffer.Length);
                                if (read <= 0) break;

                                blob.Write(buffer, read, blobOffest);
                                blobOffest += read;
                            }
                        }
                    }
                }

                filekey.Id = lastInsertId;
                return lastInsertId;
            }
        }

        public void DeleteActNo(int actNo) {
            var delete1 = "delete from t_filestore where id in (select id from t_fileinfo where act_no=?)";
            var delete2 = "delete from t_fileinfo where act_no=?";

            using (var cmd = new SQLiteCommand(conn)) {
                cmd.Parameters.Add(new SQLiteParameter() { Value = actNo });
                cmd.CommandText = delete1;
                cmd.ExecuteNonQuery();
                cmd.CommandText = delete2;
                cmd.ExecuteNonQuery();
            }
        }

        public FileInfo Select(ReplacedFile filekey) {
            return this.Select(filekey.Id);
        }

        public FileInfo Select(int id, FileInfo outTo = null) {
            using (var cmd = new SQLiteCommand(conn)) {
                cmd.CommandText = "select id as bin, length(bin) as bin_length from t_filestore where id = ?";
                cmd.Parameters.Add(new SQLiteParameter() { Value = id });
                using (var reader = cmd.ExecuteReader(System.Data.CommandBehavior.KeyInfo)) {
                    if (reader.Read()) {

                        var output = outTo ?? new FileInfo(Path.GetTempFileName());

                        int blobLength = reader.GetInt32(1);
                        int blobOffest = 0;

                        byte[] buffer = new byte[BUFFER_SIZE];

                        using (var blob = reader.GetBlob(0, false))
                        using (var writer = output.OpenWrite()) {
                            while (blobOffest < blobLength) {
                                int read = Math.Min(buffer.Length, blobLength - blobOffest);
                                blob.Read(buffer, read, blobOffest);
                                writer.Write(buffer, 0, read);

                                blobOffest += read;
                            }
                        }
                        return output;

                    } else {
                        return null;
                    }
                }
            }
        }

        public List<ReplacedFile> SelectFileInfos(int actNo) {
            using (var context = new DataContext(conn)) {
                var table = context.GetTable<ReplacedFile>();
                return table.Where(s => s.ActNo == actNo).ToList();
            }
        }
    }

    [Table(Name = "t_fileinfo")]
    class ReplacedFile {
        [Column(Name = "id", CanBeNull = false, DbType = "INT", IsPrimaryKey = true)]
        public int Id { set; get; }

        [Column(Name = "act_no", CanBeNull = false, DbType = "INT")]
        public int ActNo { set; get; }

        [Column(Name = "filepath", CanBeNull = false, DbType = "TEXT")]
        public string FilePath { set; get; }

        [Column(Name = "guarantee_hash", CanBeNull = false, DbType = "TEXT")]
        public string ReplacedFileHash { set; get; }

    }
}

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace learning.zeromq
{
    public class TransactionalTask : IPersistedTask, IEnlistmentNotification
    {
        protected class LocalTransactionScope : IDisposable
        {
            protected TransactionScope _txScope;

            public LocalTransactionScope(Transaction tx)
            {
                if (tx != null)
                {
                    _txScope = new TransactionScope(tx);

                    Transaction.Current = tx;
                }
            }

            public void Complete()
            {
                if (_txScope != null)
                {
                    _txScope.Complete();
                }
            }

            public void Dispose()
            {
                if (_txScope != null)
                {
                    _txScope.Dispose();
                    _txScope = null;
                }
            }
        }

        public byte[] TranscactionData { get; set; }

        public TransactionalTask()
        {
            this.TaskId = Guid.NewGuid().ToString();

            this.TranscactionData = SerializeMyTransaction();
        }

        protected byte[] SerializeMyTransaction()
        {
            if (Transaction.Current != null)
            {
                // Transaction.Current.EnlistDurable(Guid.Parse(this.TaskId), this, EnlistmentOptions.EnlistDuringPrepareRequired);

                BinaryFormatter formatter = new BinaryFormatter();

                using (var memStream = new System.IO.MemoryStream())
                {
                    formatter.Serialize(memStream, Transaction.Current);

                    var r = memStream.GetBuffer();

                    return r;
                }

            }
            else
            {
                return new byte[0];
            }
        }

        protected Transaction DeserializeMyTransaction()
        {
            if (this.TranscactionData != null && this.TranscactionData.Length > 0)
            {
                using (var memStream = new System.IO.MemoryStream(this.TranscactionData))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    var tx = (Transaction)formatter.Deserialize(memStream);

                    return tx;
                }
            }

            return null;
        }

        public string TaskId
        {
            get;
            set;
        }

        public void Execute()
        {
            var tx = DeserializeMyTransaction();

            using (var txScope = new LocalTransactionScope(tx))
            {
                OnExecute();

                txScope.Complete();
            }

        }

        protected virtual void OnExecute() { }

        public void Commit(Enlistment enlistment)
        {
        }

        public void InDoubt(Enlistment enlistment)
        {
        }

        public void Prepare(PreparingEnlistment preparingEnlistment)
        {
        }

        public void Rollback(Enlistment enlistment)
        {
        }
    }

    namespace Data
    {
        [Serializable]
        public class DbRecord
        {
            public int Id { get; set; }
            public string Value { get; set; }
            public string TransactionId { get; set; }
            public DateTime DateInserted { get; set; }

            public override string ToString()
            {
                return string.Format("ID:{0} | VALUE:{1} | TX: {2} | DATE: {3}", this.Id, this.Value, this.TransactionId, this.DateInserted);
            }
        }

        public abstract class DbTask : TransactionalTask
        {
            protected const string TABLE_NAME = "activity_history";

            public string Database { get; set; }

            protected string TransactionId
            {
                get
                {
                    return (Transaction.Current != null) ? Transaction.Current.TransactionInformation.DistributedIdentifier.ToString() : Guid.Empty.ToString();
                }
            }

            public DbTask(string dbName)
            {
                this.Database = dbName;
            }

            protected sealed override void OnExecute()
            {
                using (var txScope = new TransactionScope())
                {
                    using (var cnn = new SQLiteConnection(string.Format(@"Data Source=data\{0}.sqlite;Version=3;", this.Database)))
                    {
                        cnn.Open();

                        OnDbOperation(cnn);
                    }
                }
            }


            protected abstract void OnDbOperation(SQLiteConnection cnn);
        }

        [Serializable]
        public class GetRecordCount : DbTask
        {
            public int RecordCount { get; set; }

            public GetRecordCount(string db)
                : base(db)
            {

            }

            protected override void OnDbOperation(SQLiteConnection cnn)
            {
                using (var cmd = cnn.CreateCommand())
                {
                    cmd.CommandText = string.Format("select count(*) as expr1 from {0}", TABLE_NAME);

                    using (var reader = cmd.ExecuteReader())
                    {
                        reader.Read();

                        this.RecordCount = reader.GetInt32(0);
                    }
                }
            }

            public override string ToString()
            {
                return string.Format("Record Count={0}", this.RecordCount);
            }
        }

        [Serializable]
        public class AddRecord : DbTask
        {
            public DbRecord Model { get; set; }

            public AddRecord(string db)
                : base(db)
            {

            }

            protected override void OnDbOperation(SQLiteConnection cnn)
            {
                if (this.Model != null)
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        var sqlStmt = string.Format("insert into {0} (value,transaction_id) values('{0}', '{1}')", TABLE_NAME, this.Model.Value, this.TransactionId);

                        cmd.CommandText = sqlStmt;
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    throw new ArgumentException("Model is NULL");
                }
            }

            public override string ToString()
            {
                if (this.Model != null)
                {
                    return this.Model.ToString();
                }
                else
                {
                    return "NULL";
                }
            }
        }
    }
}

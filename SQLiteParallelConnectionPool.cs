/*+Copyright [2016]
 +
 +   Licensed under the Apache License, Version 2.0 (the "License");
 +   you may not use this file except in compliance with the License.
 +   You may obtain a copy of the License at
 +
 +     http://www.apache.org/licenses/LICENSE-2.0
 +
 +   Unless required by applicable law or agreed to in writing, software
 +   distributed under the License is distributed on an "AS IS" BASIS,
 +   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 +   See the License for the specific language governing permissions and
 +   
 */
 namespace SQLitePortable
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    /// <summary>
    /// Responsible to create a bridge between sql lite orm
    /// and our repository classes.
    /// </summary>
    public static class SQLiteParallelConnectionCache
    {
        /// <summary>
        /// The write connections cache
        /// </summary>
        private static readonly ConcurrentDictionary<string, ConcurrentBag<SQLiteParallelConnection>>
            WriteConnectionsCache = new ConcurrentDictionary<string, ConcurrentBag<SQLiteParallelConnection>>();

        /// <summary>
        /// The read connections cache
        /// </summary>
        private static readonly ConcurrentDictionary<string, ConcurrentBag<SQLiteParallelConnection>>
            ReadConnectionsCache = new ConcurrentDictionary<string, ConcurrentBag<SQLiteParallelConnection>>();

        /// <summary>
        /// The is enable
        /// </summary>
        public static bool IsEnable = true;

        /// <summary>
        /// Caches the specified database path.
        /// </summary>
        /// <param name="databasePath">The database path.</param>
        /// <param name="connection">The connection.</param>
        public static void Cache(string databasePath, SQLiteParallelConnection connection)
        {
            if (!IsEnable)
            {
                return;
            }

            var Cache = connection.IsReadonly ? ReadConnectionsCache : WriteConnectionsCache;

            var bag = Cache.GetOrAdd(databasePath, new ConcurrentBag<SQLiteParallelConnection>());
            bag.Add(connection);
            /*
            AppLogger.Default.WriteVerbose(
                                        "[DB] {1} connection cached count {0}",
                                        bag.Count,
                                      connection.IsReadonly ? "Read" : "Write");
            */
        }

        /// <summary>
        /// Takes the specified database path.
        /// </summary>
        /// <param name="databasePath">The database path.</param>
        /// <param name="readonlyConnection">if set to <c>true</c> [readonly connection].</param>
        /// <param name="ct">The ct.</param>
        /// <returns></returns>
        public static SQLiteParallelConnection Take(string databasePath, bool readonlyConnection, CancellationToken ct)
        {
            if (!IsEnable)
            {
                return null;
            }

            var Cache = readonlyConnection ? ReadConnectionsCache : WriteConnectionsCache;
            ct.ThrowIfCancellationRequested();

            var t = Task.Run(
                             () =>
                             {
                                 if (Cache.ContainsKey(databasePath) && Cache[databasePath].Count > 0)
                                 {
                                     /*
                                     AppLogger.Default.WriteVerbose(
                                                                 "[DB] Connection cached Take request {0}",
                                                                 Cache[databasePath].Count);
                                     */

                                     SQLiteParallelConnection ret;
                                     Cache[databasePath].TryTake(out ret);

                                     if (ct.IsCancellationRequested)
                                     {
                                         ret.Close();
                                         ct.ThrowIfCancellationRequested();
                                     }

                                     return ret;
                                 }

                                 return null;
                             },
                             ct);

            t.Wait(ct);
            return t.Result;
        }

        /// <summary>
        /// Closes all connections.
        /// this call is dangerous since is can impact all databases.
        /// </summary>
        public static void CloseAllConnections()
        {
            if (AppLogger.Default != null)
            {
                AppLogger.Default.WriteInfo($"CloseAllConnections called.");
            }

            foreach (var key in WriteConnectionsCache.Keys)
            {
                ConcurrentBag<SQLiteParallelConnection> bag;
                if (WriteConnectionsCache.TryRemove(key, out bag))
                {
                    ClearBag(bag);
                }
            }

            foreach (var key in ReadConnectionsCache.Keys)
            {
                ConcurrentBag<SQLiteParallelConnection> bag;
                if (ReadConnectionsCache.TryRemove(key, out bag))
                {
                    ClearBag(bag);
                }
            }
        }

        /// <summary>
        /// Clears the bag.
        /// </summary>
        /// <param name="bag">The bag.</param>
        private static void ClearBag(ConcurrentBag<SQLiteParallelConnection> bag)
        {
            foreach (var sqLiteParallelConnection in bag)
            {
                sqLiteParallelConnection.Close();
            }
        }
    }


    /// <summary>
    /// The connection pool object
    /// </summary>
    public class SQLiteParallelConnectionPool
    {
        /// <summary>
        /// The read connections
        /// </summary>
        private static ConcurrentDictionary<string, SemaphoreSlim> ReadConnections;

        /// <summary>
        /// The write connections
        /// </summary>
        private static ConcurrentDictionary<string, SemaphoreSlim> WriteConnections;

        /// <summary>
        /// The pool cancellation token
        /// </summary>
        private static CancellationTokenSource PoolCancellationToken;

        /// <summary>
        /// The database path
        /// </summary>
        private readonly string databasePath;

        /// <summary>
        /// The read lock
        /// </summary>
        private readonly SemaphoreSlim readLock;

        /// <summary>
        /// The write lock
        /// </summary>
        private readonly SemaphoreSlim writeLock;

        /// <summary>
        /// Initializes the <see cref="SQLiteParallelConnectionPool"/> class.
        /// </summary>
        static SQLiteParallelConnectionPool()
        {
            Reset();
        }

        /// <summary>
        /// Cancels the connections.
        /// </summary>
        public static void CancelConnections()
        {
            PoolCancellationToken.Cancel();
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        public static void Reset()
        {
            ReadConnections = new ConcurrentDictionary<string, SemaphoreSlim>();
            WriteConnections = new ConcurrentDictionary<string, SemaphoreSlim>();
            PoolCancellationToken = new CancellationTokenSource();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SQLiteParallelConnectionPool"/> class.
        /// </summary>
        /// <param name="path">The path.</param>
        public SQLiteParallelConnectionPool(string path)
        {
            this.databasePath = path;
            this.readLock = ReadConnections.GetOrAdd(this.databasePath, new SemaphoreSlim(64));
            this.writeLock = WriteConnections.GetOrAdd(this.databasePath, new SemaphoreSlim(1));
            SQLite3.SetDirectory(/*temp directory type*/2, ApplicationFolderPaths.LocalTempFolderPath);
        }

        /// <summary>
        /// Creates the database with the pragma and settings.
        /// </summary>
        /// <returns></returns>
        public async Task CreateDatabase()
        {
            // If the file already exists no need 
            if (File.Exists(this.databasePath))
            {
                return;
            }

            using (var db = await this.GetConnectionAsync(true))
            {
                var pragmaMode = db.ExecuteScalar<string>("PRAGMA journal_mode;");
                if (pragmaMode.ToLower() != "wal")
                {
                    /******************************************
                    // http://www.sqlite.org/draft/wal.html
                        There are advantages and disadvantages to using WAL instead of a rollback journal. Advantages include:
                            1.WAL is significantly faster in most scenarios. 
                            2.WAL provides more concurrency as readers do not block writers and a writer does not block readers. Reading and writing can proceed concurrently. 
                            3.Disk I/O operations tends to be more sequential using WAL. 
                            4.WAL uses many fewer fsync() operations and is thus less vulnerable to problems on systems where the fsync() system call is broken. 

                    *********************/

                    db.ExecuteScalar<string>("PRAGMA journal_mode=WAL;");
                }

                // enable foreign key support (otherwise SQLite will not guarantee integrity of foreign keys)
                // please note this depends on the version of the db used, but if in doubt set this on.
                db.Execute("PRAGMA foreign_keys=ON;");
            }

            // we should not move on if the database isn't created.
            if (!File.Exists(this.databasePath))
            {
                throw new DatabaseInitializationException($"Database not created on create Path: {this.databasePath}");
            }
        }

        /// <summary>
        /// Drops the database.
        /// this is used on testing scenarios.
        /// </summary>
        /// <returns></returns>
        public Task DropDatabase()
        {
            // File.Delete call is blocking call so it should be wrapped Task
            return Task.Run(() => File.Delete(this.databasePath));
        }

        /// <summary>
        /// Gets the connection asynchronous.
        /// </summary>
        /// <param name="writable">if set to <c>true</c> [writable].</param>
        /// <returns></returns>
        public async Task<SQLiteParallelConnection> GetConnectionAsync(bool writable)
        {
            var ct = PoolCancellationToken.Token;

            ct.ThrowIfCancellationRequested();

            if (writable)
            {
                var sw = Stopwatch.StartNew();
                await this.writeLock.WaitAsync(ct);

                // TODO shcarr, Ocassionally i have see that applogger.default is null. I don't think we want to have a static variable for logging globally assigned like this
                // also becuase this is just performance related metrics, i am adding this null check.
                if (AppLogger.Default != null)
                {
                    if (sw.ElapsedMilliseconds > 1000)
                    {
                        AppLogger.Default.WriteWarning("[DB.WAIT.WRITE] Pool lock wait time {0}ms", sw.ElapsedMilliseconds);
                    }
                    else if (sw.ElapsedMilliseconds > 100)
                    {
                        AppLogger.Default.WriteInfo("[DB.WAIT.WRITE] Pool lock wait time {0}ms", sw.ElapsedMilliseconds);
                    }
                    else if (sw.ElapsedMilliseconds > 10)
                    {
                        AppLogger.Default.WriteVerbose("[DB.WAIT.WRITE] Pool lock wait time {0}ms", sw.ElapsedMilliseconds);
                    }
                }

                return SQLiteParallelConnectionCache.Take(this.databasePath, false, ct) ??
                          CreateNew(this.writeLock,
                                    this.databasePath,
                                    SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);
            }

            var rsw = Stopwatch.StartNew();
            await this.readLock.WaitAsync(ct);

            if (AppLogger.Default != null)
            {
                if (rsw.ElapsedMilliseconds > 1000)
                {
                    AppLogger.Default.WriteWarning("[DB.WAIT.READ] Pool lock wait time {0}ms", rsw.ElapsedMilliseconds);
                }
                else if (rsw.ElapsedMilliseconds > 100)
                {
                    AppLogger.Default.WriteInfo("[DB.WAIT.READ] Pool lock wait time {0}ms", rsw.ElapsedMilliseconds);
                }
                else if (rsw.ElapsedMilliseconds > 10)
                {
                    AppLogger.Default.WriteVerbose("[DB.WAIT.READ] Pool lock wait time {0}ms", rsw.ElapsedMilliseconds);
                }
            }

            return SQLiteParallelConnectionCache.Take(this.databasePath, true, ct) ?? CreateNew(this.readLock, this.databasePath, SQLiteOpenFlags.ReadOnly);
        }

        /// <summary>
        /// Creates the new.
        /// </summary>
        /// <param name="connectionLock">The connection lock.</param>
        /// <param name="databasePath">The database path.</param>
        /// <param name="openFlags">The open flags.</param>
        /// <returns></returns>
        private static SQLiteParallelConnection CreateNew(
            SemaphoreSlim connectionLock,
            string databasePath,
            SQLiteOpenFlags openFlags)
        {
            // Only return new connection if cancellation is not requested
            if (!PoolCancellationToken.Token.IsCancellationRequested)
            {
                DelegateRetry retry = new DelegateRetry(3, new TimeSpan(0, 0, 0, 500), true /*double delay*/);

                Func<SQLiteParallelConnection> function = delegate
                {
                    return new SQLiteParallelConnection(connectionLock, databasePath, openFlags)
                    {
                        BusyTimeout = TimeSpan.FromMinutes(3)
                    };
                };

                // if there is a transient exception such as the file not getting created as we expect we run this to retry the creaton of the connection. 
                // this becomes particuarly important for the first writer which actually creates teh db today. A better design would be to do a check in the create database call 
                return retry.RetryN<SQLiteParallelConnection>(function, new Func<Exception, bool>(DelegateRetry.IsTransientSqlException));
            }

            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class SQLiteParallelConnection : SQLiteConnection
    {
        /// <summary>
        /// The connection lock
        /// </summary>
        private readonly SemaphoreSlim connectionLock;

        /// <summary>
        /// Gets a value indicating whether this instance is readonly.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is readonly; otherwise, <c>false</c>.
        /// </value>
        public bool IsReadonly { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SQLiteParallelConnection"/> class.
        /// </summary>
        /// <param name="connectionLock">The connection lock.</param>
        /// <param name="databasePath">The database path.</param>
        /// <param name="openFlags">The open flags.</param>
        /// <param name="storeDateTimeAsTicks">if set to <c>true</c> [store date time as ticks].</param>
        public SQLiteParallelConnection(
            SemaphoreSlim connectionLock,
            string databasePath,
            SQLiteOpenFlags openFlags,
            bool storeDateTimeAsTicks = false) : base(databasePath, openFlags, storeDateTimeAsTicks)
        {
            this.connectionLock = connectionLock;
            this.IsReadonly = (openFlags & SQLiteOpenFlags.ReadOnly) != 0;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposing || !SQLiteParallelConnectionCache.IsEnable)
            {
                base.Dispose(false);
            }
            else
            {
                SQLiteParallelConnectionCache.Cache(this.DatabasePath, this);
            }

            if (this.connectionLock != null)
            {
                this.connectionLock.Release();
            }
        }
    }
}
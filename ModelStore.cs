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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    public abstract class ModelStore : IModelStore
    {
        private const string DatabaseFolder = "sqlite-databases";

        private readonly SQLiteParallelConnectionPool connectionPool;

        private readonly ConcurrentDictionary<Type, bool> initialzedTypesDictionary;

        public abstract Task Initialize(bool createTables);

        public static string GetStorePath(string databaseName)
        {
            var localFolder = ApplicationFolderPaths.LocalFolderPath;
            return Path.Combine(localFolder, DatabaseFolder, databaseName);
        }

        public static void CreateDatabaseFolder()
        {
            var localFolder = ApplicationFolderPaths.LocalFolderPath;
            Directory.CreateDirectory(Path.Combine(localFolder, DatabaseFolder));
        }

        protected ModelStore(string databaseName)
        {
            this.connectionPool = new SQLiteParallelConnectionPool(GetStorePath(databaseName));
            this.initialzedTypesDictionary = new ConcurrentDictionary<Type, bool>();
        }

        public async Task<int> InitializeTableAsync<TStorageModel, TModel>() where TModel : BaseModel, new()
            where TStorageModel : BaseStorageModel<TModel>, new()
        {
            if (this.initialzedTypesDictionary.ContainsKey(typeof (TStorageModel)))
            {
                return 1;
            }

            ModelStore.CreateDatabaseFolder();
            await this.connectionPool.CreateDatabase();
            using (var db = await this.connectionPool.GetConnectionAsync(true))
            {
                db.CreateTable(typeof (TStorageModel));
                this.initialzedTypesDictionary[typeof (TStorageModel)] = true;
            }

            return 0;
        }

        public Task<int> SaveAsync<TStorageModel, TModel>(TModel model) where TModel : BaseModel, new()
            where TStorageModel : BaseStorageModel<TModel>, new()
        {
            return Task.Run(async () => {
                using (var db = await this.connectionPool.GetConnectionAsync(true))
                {
                    using (new PerformanceTimer(typeof(TStorageModel).Name + ".insert", false))
                    {
                        var storageModel = StorageModelInitializers.FromModel<TStorageModel, TModel>(model);
                        return db.InsertOrReplace(storageModel);
                    }
                }
            });
        }

        public Task<int> SaveAllAsync<TStorageModel, TModel>(ICollection<TModel> models, bool overwriteExisting = true)
            where TModel : BaseModel, new() where TStorageModel : BaseStorageModel<TModel>, new()
        {
            return Task.Run(async () => {
                using (var db = await this.connectionPool.GetConnectionAsync(true))
                {
                    return this.InternalSaveAll<TStorageModel, TModel>(db, models, overwriteExisting);
                }
            });
        }

        public Task Delete<TStorageModel, TModel>(TModel model) where TModel : BaseModel
            where TStorageModel : BaseStorageModel<TModel>
        {
            return this.WithDBAsync(db => {
                using (new PerformanceTimer(typeof (TStorageModel).Name + ".delete", false))
                {
                    var storageModel = StorageModelInitializers.FromModel<TStorageModel, TModel>(model);
                    db.Delete(storageModel);
                }
            },
            true);
        }

        public Task DeleteAll<TStorageModel, TModel>() where TModel : BaseModel
            where TStorageModel : BaseStorageModel<TModel>
        {
            return this.WithDBAsync(db => {
                using (new PerformanceTimer(typeof (TStorageModel).Name + ".batch.delete", true))
                {
                    db.DeleteAll<TStorageModel>();
                }
            },
            true);
        }

        public Task<List<TModel>> FindAsync<TStorageModel, TModel>(
            Expression<Func<TStorageModel, bool>> filterExpression,
            int offset = -1,
            int limit = -1) where TModel : BaseModel, new() where TStorageModel : BaseStorageModel<TModel>, new()
        {
            return Task.Run(async () => {
                using (var db = await this.connectionPool.GetConnectionAsync(false))
                {
                    using (new PerformanceTimer(typeof (TStorageModel).Name + ".find", true))
                    {
                        TableQuery<TStorageModel> tableQuery = db.Table<TStorageModel>().Where(filterExpression);
                        if (offset >= 0)
                        {
                            tableQuery = tableQuery.Skip(offset);
                        }

                        if (limit > 0)
                        {
                            tableQuery = tableQuery.Take(limit);
                        }

                        var index_models = tableQuery.Select(s => s).ToList();
                        return index_models.Select(StorageModelInitializers.FromStorageModel<TStorageModel, TModel>)
                                            .ToList<TModel>();
                    }
                }
            });
        }

        public Task WithTableAsync<TStorageModel, TModel>(Func<TableQuery<TStorageModel>, Task> callback)
            where TModel : BaseModel, new() where TStorageModel : BaseStorageModel<TModel>, new()
        {
            return Task.Run(async () => {
                using (var db = await this.connectionPool.GetConnectionAsync(false))
                {
                    using (new PerformanceTimer(typeof (TStorageModel).Name + ".with.table", true))
                    {
                        return callback(db.Table<TStorageModel>());
                    }
                }
            });
        }

        public Task WithDBAsync(Action<SQLiteParallelConnection> callback, bool writable = false)
        {
            return Task.Run(async () => {
                using (var db = await this.connectionPool.GetConnectionAsync(writable))
                {
                    await this.InvokeWithRetry(callback, db);
                }
            });
        }

        private async Task InvokeWithRetry(Action<SQLiteParallelConnection> callback, SQLiteParallelConnection db)
        {
            int retryAttempts = 3;
            while (retryAttempts >= 0)
            {
                SQLiteException exception = null;
                try
                {
                    callback(db);
                }
                catch (SQLiteException e)
                {
                    if (retryAttempts <= 0 || 
                        (e.Result != SQLite3.Result.CannotOpen && 
                         e.Result != SQLite3.Result.LockErr && 
                         e.Result != SQLite3.Result.Warning))
                    {
                        throw;
                    }

                    exception = e;
                }

                if (exception == null)
                {
                    return;
                }
                
                await Task.Delay(500 * retryAttempts);
                retryAttempts--;
            }
        }

        private int InternalSaveAll<TStorageModel, TModel>(
            SQLiteParallelConnection db,
            ICollection<TModel> models,
            bool overwriteExisting = true) where TModel : BaseModel, new()
            where TStorageModel : BaseStorageModel<TModel>, new()
        {
            using (new PerformanceTimer(typeof (TStorageModel).Name + ".batch.insert", true))
            {
                int rowCount = 0;
                try
                {
                    db.BeginTransaction();
                    foreach (var model in models)
                    {
                        TStorageModel storageModel = StorageModelInitializers.FromModel<TStorageModel, TModel>(model);
                        if (overwriteExisting)
                        {
                            rowCount += db.InsertOrReplace(storageModel);
                        }
                        else
                        {
                            rowCount += db.Insert(storageModel);
                        }
                    }
                    db.Commit();
                }
                catch (Exception)
                {
                    db.Rollback();
                    rowCount = -1;
                }

                return rowCount;
            }
        }
    }
}
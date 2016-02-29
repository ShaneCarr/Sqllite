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
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using SQLitePortable;

    public interface IModelStore
    {
        Task Delete<TStorageModel, TModel>(TModel model) where TStorageModel : BaseStorageModel<TModel>
            where TModel : BaseModel;

        Task DeleteAll<TStorageModel, TModel>() where TStorageModel : BaseStorageModel<TModel> where TModel : BaseModel;

        Task<List<TModel>> FindAsync<TStorageModel, TModel>(
            Expression<Func<TStorageModel, bool>> filterExpression,
            int offset = -1,
            int limit = -1) where TStorageModel : BaseStorageModel<TModel>, new() where TModel : BaseModel, new();

        Task<int> InitializeTableAsync<TStorageModel, TModel>() where TStorageModel : BaseStorageModel<TModel>, new()
            where TModel : BaseModel, new();

        Task<int> SaveAllAsync<TStorageModel, TModel>(ICollection<TModel> models, bool overwriteExisting = true)
            where TStorageModel : BaseStorageModel<TModel>, new() where TModel : BaseModel, new();

        Task<int> SaveAsync<TStorageModel, TModel>(TModel model) where TStorageModel : BaseStorageModel<TModel>, new()
            where TModel : BaseModel, new();

        Task WithDBAsync(Action<SQLiteParallelConnection> callback, bool writable = false);

        Task WithTableAsync<TStorageModel, TModel>(Func<TableQuery<TStorageModel>, Task> callback)
            where TStorageModel : BaseStorageModel<TModel>, new() where TModel : BaseModel, new();
    }
}
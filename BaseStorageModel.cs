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
    using SQLitePortable;

    /// <summary>
    /// The storage models are used by SQLite.NET's lightweight ORM in caching a domain model in the local store.
    /// </summary>
    /// <typeparam name="TModel">Type of model serialized in local storage.</typeparam>
    public abstract class BaseStorageModel<TModel> where TModel : BaseModel
    {
        [Ignore]
        public TModel Model { get; protected set; }

        [Column("SerializedModel")]
        [NotNull]
        public byte[] SerializedModelData
        {
            get { return this.SerializeModel(); }
            set { this.Model = this.DeserializeModel(value); }
        }

        protected virtual byte[] SerializeModel()
        {
            if (this.Model == null)
            {
                return null;
            }

            return JsonHelper.SerializeObjectToBytes(this.Model);
        }

        protected virtual TModel DeserializeModel(byte[] serializedModel)
        {
            if (serializedModel == null || serializedModel.Length == 0)
            {
                return null;
            }

            return JsonHelper.DeserializeObjectFromBytes<TModel>(serializedModel);
        }
    }
}
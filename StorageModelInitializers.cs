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
    using Model;
    
    public static class StorageModelInitializers
    {
        private static readonly Dictionary<Type, object> StorageToModelInitializers = new Dictionary<Type, object>();

        private static readonly Dictionary<Type, object> ModelToStorageInitializers = new Dictionary<Type, object>();

        public static void ToModelInitializer<TStorageModel, TModel>(Func<TStorageModel, TModel> callback)
            where TStorageModel : BaseStorageModel<TModel> where TModel : BaseModel
        {
            StorageToModelInitializers[typeof(TStorageModel)] = callback;
        }

        public static void ToStorageModelInitializer<TStorageModel, TModel>(Func<TModel, TStorageModel> callback)
            where TStorageModel : BaseStorageModel<TModel>, new() where TModel : BaseModel
        {
            ModelToStorageInitializers[typeof(TStorageModel)] = callback;
        }

        public static TStorageModel FromModel<TStorageModel, TModel>(TModel model)
            where TStorageModel : BaseStorageModel<TModel> where TModel : BaseModel
        {
            if (ModelToStorageInitializers.ContainsKey(typeof(TStorageModel)))
            {
                Func<TModel, TStorageModel> callback =
                    (Func<TModel, TStorageModel>)ModelToStorageInitializers[typeof(TStorageModel)];
                return callback(model);
            }

            throw new KeyNotFoundException(
                string.Format(
                    "Converter has not been registered for {0} -> {1}",
                    model.GetType(),
                    typeof(TStorageModel)));
        }


        public static TModel FromStorageModel<TStorageModel, TModel>(TStorageModel storageModel)
            where TStorageModel : BaseStorageModel<TModel> where TModel : BaseModel
        {
            if (StorageToModelInitializers.ContainsKey(typeof(TStorageModel)))
            {
                Func<TStorageModel, TModel> callback =
                    (Func<TStorageModel, TModel>)StorageToModelInitializers[typeof(TStorageModel)];
                return callback(storageModel);
            }

            throw new KeyNotFoundException(
                string.Format(
                    "Converter has not been registered for {0} -> {1}",
                    storageModel.GetType(),
                    typeof(TModel)));
        }
    }
}
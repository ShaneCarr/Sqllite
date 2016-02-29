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

    internal class StorageBaseException : DataAccessException
    {
        public StorageBaseException(string message) : base(message)
        {
        }

        public StorageBaseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    internal class StoragePrepareException : StorageBaseException
    {
        public StoragePrepareException(string message) : base(message)
        {
        }

        public StoragePrepareException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    internal class StorageUpdateException : StorageBaseException
    {
        public StorageUpdateException(string message) : base(message)
        {
        }

        public StorageUpdateException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
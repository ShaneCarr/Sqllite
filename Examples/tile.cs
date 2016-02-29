///example of query
        public async Task<IEnumerable<IConversation>> SelectConversationWithExpression(Expression<Func<IConversationStorageModel, bool>> filterExpression, int conversationCount)
        {
            Expression<Func<ConversationStorageModel, bool>> lambda = Expression.Lambda<Func<ConversationStorageModel, bool>>(filterExpression.Body, filterExpression.Parameters);
            IEnumerable<IConversation> conversation = await this.database.SelectAsync<ConversationStorageModel, Conversation>(lambda);
            return conversation;
        }

///a storage model

    [Table(TableName)]
    public class Tile : BaseStorageModel<Tile>
    {
        /// <summary>
        /// The table name
        /// </summary>
        public const string TableName = "DM_SomeScreenTile";

        [Column(Columns.UniqueId), PrimaryKey, NotNull]
        public string UniqueId { get; set; }

        [Column(Columns.someimportantidId), Indexed(Name = "DM_SomeScreenTile_someimportantidId", Unique = true), NotNull]
        public string someimportantidId { get; set; }

        [Column(Columns.someimportantidSomeScreenTileTileStatus), Indexed(Name = "DM_SomeScreenTile_someimportantidSomeScreenTileTileStatus", Unique = false)]
        public SomeScreenTileTileStatus someimportantidSomeScreenTileTileStatus { get; set; }

        [Column(Columns.ConversationUniqueId), 
            Indexed(Name = "DM_SomeScreenTile_ConversationUniqueId", Unique = true), 
            NotNull, ForeignKey(ConversationStorageModel.TableName, ConversationStorageModel.Columns.UniqueId, 
            Name = "FK_ConversationId", Unique = true)]
        public string ConversationUniqueId { get; set; }

        public static void RegisterInitializers()
        {
            // Note i am double storing some columns. Add Jsonignore later so we don' t stored the
            // serialized data along with the first class columns
            StorageModelInitializers.ToStorageModelInitializer<SomeScreenTileTileStorageModel, SomeScreenTileTile>(
                m => new SomeScreenTileTileStorageModel
                {
                    UniqueId = m.UniqueId ?? System.Guid.NewGuid().ToString(),
                    someimportantidId = m.someimportantidId,
                    someimportantidSomeScreenTileTileStatus = m.someimportantidSomeScreenTileTileStatus,
                    ConversationUniqueId = m.ConversationUniqueId,
                    Model = m,
                });

            StorageModelInitializers.ToModelInitializer<SomeScreenTileTileStorageModel, SomeScreenTileTile>(
                s =>
                {
                    var m = s.Model;
                    m.UniqueId = s.UniqueId;
                    m.someimportantidId = s.someimportantidId;
                    m.someimportantidSomeScreenTileTileStatus = s.someimportantidSomeScreenTileTileStatus;
                    m.ConversationUniqueId = s.ConversationUniqueId;
                    return m;
                });
        }

        /// <summary>
        /// the columns class
        /// </summary>
        public class Columns
        {
            /// <summary>
            /// The unique identifier
            /// </summary>
            public const string UniqueId = "UniqueId";

            /// <summary>
            /// The conversation item identifier
            /// </summary>
            public const string someimportantidId = "someimportantidId";

            /// <summary>
            /// The someimportantid home screen tile status
            /// </summary>
            public const string someimportantidSomeScreenTileTileStatus = "someimportantidSomeScreenTileTileStatus";

            /// <summary>
            /// The conversation unique identifier
            /// </summary>
            public const string ConversationUniqueId = "ConversationUniqueId";
        }
    }
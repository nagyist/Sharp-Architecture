namespace SharpArch.RavenDb
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Domain.PersistenceSupport;
    using JetBrains.Annotations;
    using Raven.Client.Documents.Session;
    using IsolationLevel = System.Data.IsolationLevel;


    /// <summary>
    ///     Transaction Manager for RavenDB.
    /// </summary>
    /// <remarks>
    ///     This implementation is based on <see cref="TransactionScope" />.
    /// </remarks>
    /// <seealso cref="SharpArch.Domain.PersistenceSupport.ITransactionManager" />
    [PublicAPI]
    public class TransactionManager : ITransactionManager, ISupportsTransactionStatus
    {
        [NotNull] readonly IAsyncDocumentSession _session;

        [CanBeNull] TransactionScope _transaction;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TransactionManager" /> class.
        /// </summary>
        /// <param name="session">The document session.</param>
        public TransactionManager([NotNull] IAsyncDocumentSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <inheritdoc />
        public bool IsActive => _transaction != null;

        /// <summary>
        ///     Begins the transaction.
        /// </summary>
        /// <param name="isolationLevel">Transaction isolation level, see <see cref="System.Data.IsolationLevel" /> for details. </param>
        /// <remarks>
        ///     TransactionManager will single TransactionScope, subsequent call to <see cref="BeginTransaction" /> will return
        ///     same scope
        ///     until <see cref="CommitTransactionAsync" /> or <see cref="RollbackTransactionAsync" /> is called.
        /// </remarks>
        /// <returns>The transaction instance.</returns>
        public IDisposable BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
            => _transaction ??= new TransactionScope(TransactionScopeOption.Required,
                new TransactionOptions
                {
                    IsolationLevel = MapIsolationLevel(isolationLevel)
                }, TransactionScopeAsyncFlowOption.Enabled);

        /// <inheritdoc />
        /// <exception cref="T:System.InvalidOperationException">Transaction was not started.</exception>
        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction == null)
                throw new InvalidOperationException("Transaction was not started, make sure there is matching BeginTransaction() call.");
            await _session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _transaction.Complete();
            ClearTransaction();
        }

        /// <inheritdoc />
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            ClearTransaction();
            return Task.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static System.Transactions.IsolationLevel MapIsolationLevel(IsolationLevel isolationLevel)
        {
            switch (isolationLevel)
            {
                case IsolationLevel.Unspecified:
                    return System.Transactions.IsolationLevel.Unspecified;
                case IsolationLevel.Chaos:
                    return System.Transactions.IsolationLevel.Chaos;
                case IsolationLevel.ReadUncommitted:
                    return System.Transactions.IsolationLevel.ReadUncommitted;
                case IsolationLevel.ReadCommitted:
                    return System.Transactions.IsolationLevel.ReadCommitted;
                case IsolationLevel.RepeatableRead:
                    return System.Transactions.IsolationLevel.RepeatableRead;
                case IsolationLevel.Serializable:
                    return System.Transactions.IsolationLevel.Serializable;
                case IsolationLevel.Snapshot:
                    return System.Transactions.IsolationLevel.Snapshot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(isolationLevel), isolationLevel, null);
            }
        }

        void ClearTransaction()
        {
            _transaction?.Dispose();
            _transaction = null;
        }

    }
}

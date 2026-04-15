using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Core.Models.Abstraction;

public enum GitErrorType
{
    RepositoryNotFound,
    AuthenticationFailed,
    MergeConflict,
    NetworkError,
    InvalidOperation,
    NothingToCommit,
    RemoteRejected,
    Unknown
}

using System.Data.Common;

namespace PensQuiz.Api.Data;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}

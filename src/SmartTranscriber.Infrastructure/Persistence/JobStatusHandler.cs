using System.Data;

using Dapper;

using SmartTranscriber.Core.Domain;

namespace SmartTranscriber.Infrastructure.Persistence;

public sealed class JobStatusHandler : SqlMapper.TypeHandler<JobStatus>
{
    public override JobStatus Parse(object value)
    {
        return Enum.Parse<JobStatus>((string)value, ignoreCase: true);
    }

    public override void SetValue(IDbDataParameter parameter, JobStatus value)
    {
        parameter.Value = value.ToString();
        parameter.DbType = DbType.String;
    }
}

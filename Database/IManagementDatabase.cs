using System.Data;

namespace DammaniAPI.Database;

public interface IManagementDatabase
{
    IDbConnection Open();
}

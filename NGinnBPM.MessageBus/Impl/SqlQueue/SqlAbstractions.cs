
using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Data;

namespace NGinnBPM.MessageBus.Impl.SqlQueue
{
    public class SqlAbstract_sqlserver : ISqlAbstractions
    {
        /// <summary>
        /// 'normalize' parameter name
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
		public virtual string NormName(string n)
        {
            return n.StartsWith("@") ? n.Substring(1) : n;
        }
		
		public void AddParameter(DbCommand cmd, string parameterAlias, string value)
		{
			var prm = cmd.CreateParameter();
			prm.ParameterName = NormName(parameterAlias);
            prm.Value = value == null ? SqlString.Null : value;
            prm.DbType = DbType.AnsiString;
            cmd.Parameters.Add(prm);
		}
		public void AddParameter(DbCommand cmd, string parameterAlias, int? value)
		{
			IDataParameter para = cmd.CreateParameter();
            para.DbType = DbType.Int32;
            para.Value = value.HasValue ? new SqlInt32(value.Value) : SqlInt32.Null;
            para.Direction = ParameterDirection.Input;
            para.ParameterName = NormName(parameterAlias);
            cmd.Parameters.Add(para);
		}
		public void AddParameter(DbCommand cmd, string parameterAlias, DateTime? value)
		{
			IDataParameter para = cmd.CreateParameter();
            para.DbType = DbType.DateTime;
            para.Value = value.HasValue ? new SqlDateTime(value.Value) : SqlDateTime.Null;
            para.Direction = ParameterDirection.Input;
            para.ParameterName = NormName(parameterAlias);
            cmd.Parameters.Add(para);
		}
		public void AddParameter(DbCommand cmd, string parameterAlias, long? value)
		{
			IDataParameter para = cmd.CreateParameter();
            para.DbType = DbType.Int64;
            para.Value = value.HasValue ? new SqlInt64(value.Value) : SqlInt64.Null;
            para.Direction = ParameterDirection.Input;
            para.ParameterName = NormName(parameterAlias);
            cmd.Parameters.Add(para);
		}
		
		public void ExecuteDDLBatch(DbConnection con, string query)
		{
		    using (var cmd = con.CreateCommand())
		    {
		        cmd.CommandText = query;
		        cmd.ExecuteNonQuery();
		    }
		}
		
        
    }
    
    
    
    
    public class SqlAbstract_oracle : SqlAbstract_sqlserver
    {
        public override string NormName(string n)
        {
            return n.StartsWith(":") ? n.Substring(1) : n;
        }
		
    }
    
    
    
    
}

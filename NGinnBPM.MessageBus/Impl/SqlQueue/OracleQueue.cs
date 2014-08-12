/*
 * Created by SharpDevelop.
 * User: Rafal
 * Date: 2014-08-11
 * Time: 22:26
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;

namespace NGinnBPM.MessageBus.Impl.SqlQueue
{
	/// <summary>
	/// Description of OracleQueue.
	/// </summary>
	public class OracleQueue : SqlQueueBase
	{
		public OracleQueue()
		{
		}
		
		protected override string GetSqlFormatString(string fid)
		{
			switch(fid)
			{
				default:
					return base.GetSqlFormatString(fid);
			}
		}
	}
}

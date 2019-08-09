using iCuboid.HRMS.DataConnector.Models;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iCuboid.HRMS.DataConnector
{
    public static class AMSConnector
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static SqlConnection connection { get; set; }
        private static SqlCommand command { get; set; }
        private static SqlDataReader dataReader { get; set; }
        private static List<Employee> Employees { get; set; }

        public async static Task Connect()
        {

            try
            {
                Employees = new List<Employee>();
                string connetionString = ConfigurationManager.ConnectionStrings["netxsEntities"].ConnectionString.ToString();
                connection = new SqlConnection(connetionString);
                connection.Open();
                string sql = "SELECT * FROM Emp";
                command = new SqlCommand(sql, connection);
                dataReader = command.ExecuteReader();
                var datatable = new DataTable();
                datatable.Load(dataReader);
                string JsonResponse = string.Empty;
                JsonResponse = JsonConvert.SerializeObject(datatable);
                Employees = JsonConvert.DeserializeObject<List<Employee>>(JsonResponse);
                await MarkTheAttendance(Employees);
                dataReader.Close();
                command.Dispose();


            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private async static Task MarkTheAttendance(List<Employee> Employees)
        {
            DateTime LastProcessedDate = DateTime.MinValue;
            DateTime ProcessingDate = DateTime.MinValue;
            DateTime ProcessingDayCheckIn = DateTime.MinValue;
            DateTime ProcessingDayCheckOut = DateTime.MinValue;
            String EmployeName = "";
            foreach (var emp in Employees)
            {

                //Get the Last updated date of this employee
                LastProcessedDate = await HrmsConnector.CheckTheLastUpdatedAttendance(emp.EmpID);
                ProcessingDate = LastProcessedDate.AddDays(1);

                if (LastProcessedDate != DateTime.MinValue && ProcessingDate.Date != DateTime.Now.Date)
                {
                    string sql = $"select min(dt) as CheckIn,max(dt) as CheckOut,empname  from [netxs].[dbo].[Trans] where EmpID='{emp.EmpID}' and CAST(dt as date) = CAST('{ProcessingDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}' as date)  group by EmpID,empname";
                    command = new SqlCommand(sql, connection);
                    dataReader = command.ExecuteReader();
                    while (dataReader.Read())
                    {
                        ProcessingDayCheckIn = (DateTime)dataReader.GetValue(0);
                        ProcessingDayCheckOut = (DateTime)dataReader.GetValue(1);
                        EmployeName = (string)dataReader.GetValue(2);
                    }
                    await HrmsConnector.UpdateAttendance(ProcessingDate, ProcessingDayCheckIn, ProcessingDayCheckOut, emp.EmpID, EmployeName);
                }
            }
        }



        public async static Task ConnectorClose()
        {

            connection.Close();

        }


    }
}

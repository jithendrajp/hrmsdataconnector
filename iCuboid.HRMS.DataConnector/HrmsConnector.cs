using iCuboid.HRMS.DataConnector.Enums;
using iCuboid.HRMS.DataConnector.Models;
using iCuboid.HRMS.DataConnector.PublicTypes;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iCuboid.HRMS.DataConnector
{
    public static class HrmsConnector
    {
        private static readonly string _DOMAIN= ConfigurationManager.AppSettings["ErpNextDomain"];
        private static readonly string _USERNAME = ConfigurationManager.AppSettings["ErpNextUserName"];
        private static readonly string _PASSWORD = ConfigurationManager.AppSettings["ErpNextPassword"];

        private static readonly ERPNextClient Client = new ERPNextClient(_DOMAIN, _USERNAME,_PASSWORD);

        public async  static Task<DateTime> CheckTheLastUpdatedAttendance(string empid)
        {
            DateTime LastUpdatedAttendanceDt = DateTime.MinValue;
            FetchListOption listOption = new FetchListOption();
            listOption.Filters.Add(new ERPFilter(DocType.Employee, "name", OperatorFilter.Equals, empid));
            listOption.IncludedFields.AddRange(new string[] { "name", "employee_name", "date_of_joining", "last_attendance_date" });
            var employees= Client.ListObjects(DocType.Employee, listOption);
            if (employees != null && employees.Count > 0) {
                //For the first time ReadOnly field  last_attendance_date will be null
                if (employees[0].Data.last_attendance_date==null) {
                    LastUpdatedAttendanceDt = DateTime.Now.AddDays(-2);
                }
                else
                LastUpdatedAttendanceDt =DateTime.Parse(employees[0].Data.last_attendance_date);

            }

            return LastUpdatedAttendanceDt;
        }
        public  async static Task  UpdateAttendance(DateTime ProcessingDate, DateTime ProcessingDayCheckIn, DateTime ProcessingDayCheckOut, string empid,string epmname) {

            var employee=Client.GetObject(DocType.Employee, empid);
            if (employee != null)
            {
                float workinghours=0;
                ERPAttendance attendance = new ERPAttendance();
                //If there is no records in AMS for the day
                if (ProcessingDayCheckOut == DateTime.MinValue)
                {
                    //Check the employee is on leave or not
                    FetchListOption listOption = new FetchListOption();
                    listOption.Filters.Add(new ERPFilter(DocType.Attendance, "employee_id", OperatorFilter.Equals, empid));
                    listOption.Filters.Add(new ERPFilter(DocType.Attendance, "attendance_date", OperatorFilter.Equals, ProcessingDate.ToString("yyyy-MM-dd")));
                    listOption.IncludedFields.AddRange(new string[] { "name", "status", "attendance_date" });
                    var documents = Client.ListObjects(DocType.Attendance, listOption);


                    if (documents.Count > 0 && documents != null)
                    {

                        attendance.status = (AttendanceStatusEnum)Enum.Parse(typeof(AttendanceStatusEnum),documents[0].Data.status.Replace(" ", ""));
                        attendance.attendance_date = documents[0].Data.attendance_date.ToString();
                    }
                    else
                    {
                        //Not on Leave and also there is no swipe
                        attendance.status = AttendanceStatusEnum.Absent;
                        attendance.attendance_date = ProcessingDate.ToString("yyyy-MM-dd");
                    }
                }

                //if checkout is before 1 pm
                else if (ProcessingDayCheckOut < ProcessingDate.Date.AddHours(13))
                {
                    attendance.status = AttendanceStatusEnum.HalfDay;
                    attendance.attendance_date = ProcessingDayCheckOut.ToString("yyyy-MM-dd");
                    workinghours=(float)(ProcessingDayCheckOut - ProcessingDayCheckIn).TotalHours;

                }
                //if checkout is after 5 pm
                else if (ProcessingDayCheckOut > ProcessingDate.Date.AddHours(17))
                {
                    attendance.status = AttendanceStatusEnum.Present;
                    attendance.attendance_date = ProcessingDayCheckOut.ToString("yyyy-MM-dd");
                    workinghours = (float)(ProcessingDayCheckOut - ProcessingDayCheckIn).TotalHours;
                }
                attendance.employee_name = employee.Data.employee_name;
                attendance.employee = employee.Data.employee;
                attendance.company = employee.Data.company;
                attendance.department = employee.Data.department;

                //mark the attendance
                Client.InsertObject(attendance.Object);


                //create the time sheet for the employee
                ERPTimesheet timesheet = new ERPTimesheet();
                timesheet.employee_name = employee.Data.employee_name;
                timesheet.employee = employee.Data.employee;
                timesheet.company = employee.Data.company;
                timesheet.department = employee.Data.department;
                timesheet.total_hours = workinghours; 
                Client.InsertObject(timesheet.Object);


                //finally update Employee with lastattendance date
                ERPObject updated_obj = new ERPObject(DocType.Employee);
                updated_obj.Data.last_attendance_date = ProcessingDate.ToString("yyyy-MM-dd");
                Client.UpdateObject(DocType.Employee, empid, updated_obj);
            }
        }

    }
}

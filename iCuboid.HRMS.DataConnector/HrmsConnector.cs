using iCuboid.HRMS.DataConnector.Enums;
using iCuboid.HRMS.DataConnector.Models;
using iCuboid.HRMS.DataConnector.PublicTypes;
using log4net;
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
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string _DOMAIN= ConfigurationManager.AppSettings["ErpNextDomain"];
        private static readonly string _USERNAME = ConfigurationManager.AppSettings["ErpNextUserName"];
        private static readonly string _PASSWORD = ConfigurationManager.AppSettings["ErpNextPassword"];

        private static readonly ERPNextClient Client = new ERPNextClient(_DOMAIN, _USERNAME,_PASSWORD);

        public async  static Task<DateTime> CheckTheLastUpdatedAttendance(string empid)
        {
            Log.Info($"Checking LastUpdatedAttendanceDate of employee:{empid}");
            DateTime LastUpdatedAttendanceDt = DateTime.MinValue;
            FetchListOption listOption = new FetchListOption();
            listOption.Filters.Add(new ERPFilter(DocType.Employee, "name", OperatorFilter.Equals, empid));
            listOption.IncludedFields.AddRange(new string[] { "name", "employee_name", "date_of_joining", "last_attendance_date" });
            var employees= Client.ListObjects(DocType.Employee, listOption);
            if (employees != null && employees.Count > 0) {
                //For the first time ReadOnly field  last_attendance_date will be null
                if (employees[0].Data.last_attendance_date == null)
                {
                    LastUpdatedAttendanceDt = DateTime.Now.AddDays(-2);
                }
                else
                {
                    LastUpdatedAttendanceDt = DateTime.Parse(employees[0].Data.last_attendance_date);
                }
            }
            Log.Info($"LastUpdatedAttendanceDate of employee:{empid} is {LastUpdatedAttendanceDt}");
            return LastUpdatedAttendanceDt;
        }
        public async static Task UpdateAttendance(DateTime ProcessingDate, DateTime ProcessingDayCheckIn, DateTime ProcessingDayCheckOut, string empid)
        {

            try
            {
                var employee = Client.GetObject(DocType.Employee, empid);
                if (employee != null)
                {
                    Log.Info($"updating attendance of employee:{empid} for the day {ProcessingDate} with chekin {ProcessingDayCheckIn} and checkout{ProcessingDayCheckOut}");
                    int HalfDayThreshold = Convert.ToInt32(ConfigurationManager.AppSettings["HalfDayThresholdHour"]);
                    int FullDayThreshold = Convert.ToInt32(ConfigurationManager.AppSettings["FullDayThresholdHour"]);
                    float workinghours = 0;
                    float othours = 0;
                    ERPAttendance attendance = new ERPAttendance();
                    List<TimeSheetLog> TimeSheetLogs = new List<TimeSheetLog>();
                    TimeSheetLog timeSheetLog = new TimeSheetLog();
                    //If there is no records in AMS for the day
                    if (ProcessingDayCheckOut == DateTime.MinValue)
                    {
                        //Check the employee is on leave or not
                        Log.Info($"Checking employee {empid} is leave or not");
                        FetchListOption listOption = new FetchListOption();
                        listOption.Filters.Add(new ERPFilter(DocType.Attendance, "employee_id", OperatorFilter.Equals, empid));
                        listOption.Filters.Add(new ERPFilter(DocType.Attendance, "attendance_date", OperatorFilter.Equals, ProcessingDate.ToString("yyyy-MM-dd")));
                        listOption.IncludedFields.AddRange(new string[] { "name", "status", "attendance_date" });
                        var documents = Client.ListObjects(DocType.Attendance, listOption);


                        if (documents.Count > 0 && documents != null)
                        {

                            attendance.status = (AttendanceStatusEnum)Enum.Parse(typeof(AttendanceStatusEnum), documents[0].Data.status.Replace(" ", ""));
                            attendance.attendance_date = documents[0].Data.attendance_date.ToString();
                            //timeSheetLog.from_time = documents[0].Data.attendance_date.ToString();
                            //timeSheetLog.hours = 0;
                            //timeSheetLog.activity_type = "Execution";
                            //TimeSheetLogs.Add(timeSheetLog);
                            Log.Info($"Employee {empid} is on leave with status {attendance.status}");
                        }
                        else
                        {
                            //Not on Leave and also there is no swipe
                            attendance.status = AttendanceStatusEnum.Absent;
                            attendance.attendance_date = ProcessingDate.ToString("yyyy-MM-dd");
                            //timeSheetLog.from_time = ProcessingDate.ToString("yyyy-MM-dd");
                            //timeSheetLog.hours = 0;
                            //timeSheetLog.activity_type = "Execution";
                            //TimeSheetLogs.Add(timeSheetLog);
                            Log.Info($"Employee {empid} is Not on leave and there is no swipe so status :{attendance.status}");
                        }
                    }

                    //if checkout is before 1 pm
                    else if (ProcessingDayCheckOut < ProcessingDate.Date.AddHours(HalfDayThreshold))
                    {
                        attendance.status = AttendanceStatusEnum.Absent;
                        attendance.attendance_date = ProcessingDayCheckOut.ToString("yyyy-MM-dd");
                        //workinghours=(float)(ProcessingDayCheckOut - ProcessingDayCheckIn).TotalHours;
                        //timeSheetLog.from_time = ProcessingDayCheckIn.ToString("yyyy-MM-dd");
                        //timeSheetLog.to_time = ProcessingDayCheckOut.ToString("yyyy-MM-dd");
                        //var ts = (ProcessingDayCheckOut - ProcessingDayCheckIn);
                        //var h = Math.Floor(ts.TotalHours);
                        //var m = (ts.TotalHours - h) * 60;
                        //workinghours = (float)(h + m/60);
                        //timeSheetLog.hours = workinghours;
                        //timeSheetLog.activity_type = "Execution";
                        //TimeSheetLogs.Add(timeSheetLog);
                        Log.Info($"Employee {empid} checkout {ProcessingDayCheckOut} before 1pm so marking as leave with status {attendance.status}");


                    }
                    //if checkout is before 5 pm
                    else if (ProcessingDayCheckOut < ProcessingDate.Date.AddHours(FullDayThreshold))
                    {
                        attendance.status = AttendanceStatusEnum.HalfDay;
                        attendance.attendance_date = ProcessingDayCheckOut.ToString("yyyy-MM-dd");
                        //var ts = (ProcessingDayCheckOut - ProcessingDayCheckIn);
                        //var h = Math.Floor(ts.TotalHours);
                        //var m = (ts.TotalHours - h) * 60;
                        //workinghours = (float)(h + m/60);
                        //timeSheetLog.from_time = ProcessingDayCheckIn.ToString("yyyy-MM-dd");
                        //timeSheetLog.to_time = ProcessingDayCheckOut.ToString("yyyy-MM-dd");
                        //timeSheetLog.hours = workinghours;
                        //timeSheetLog.activity_type = "Execution";
                        //TimeSheetLogs.Add(timeSheetLog);
                        Log.Info($"Employee {empid}  chekout {ProcessingDayCheckOut} before 5pm so marking attendance status as {attendance.status}");

                    }
                    else
                    {
                        attendance.status = AttendanceStatusEnum.Present;
                        attendance.attendance_date = ProcessingDayCheckOut.ToString("yyyy-MM-dd");
                        // workinghours = (float)(ProcessingDayCheckOut - ProcessingDayCheckIn).TotalHours;
                        var ts = (ProcessingDayCheckOut - ProcessingDayCheckIn);
                        var h = Math.Floor(ts.TotalHours);
                        var m = (ts.TotalHours - h) * 60;
                        workinghours = (float)(h + m/60);
                        timeSheetLog.from_time = ProcessingDayCheckIn.ToString("yyyy-MM-dd");
                        timeSheetLog.to_time = ProcessingDayCheckOut.ToString("yyyy-MM-dd");
                        if (workinghours > 8)
                        {
                            othours = workinghours - 8;
                        }
                        else
                            othours = 0;

                        timeSheetLog.hours = othours;
                        timeSheetLog.activity_type = "Execution";
                        TimeSheetLogs.Add(timeSheetLog);
                        Log.Info($"Employee {empid} checkout {ProcessingDayCheckOut} properly and marking attendance status {attendance.status} and workinghours {timeSheetLog.hours}");
                    }
                    attendance.employee_name = employee.Data.employee_name;
                    attendance.employee = employee.Data.employee;
                    attendance.company = employee.Data.company;
                    attendance.department = employee.Data.department;
                    attendance.docstatus = 1;

                    //mark the attendance
                    Client.InsertObject(attendance.Object);
                    Log.Info($"Marked the attendance for the eemployee {empid} successfully");

                    //create the time sheet for the employee
                    if (TimeSheetLogs.Count > 0)
                    {
                        ERPTimesheet timesheet = new ERPTimesheet();
                        timesheet.employee_name = employee.Data.employee_name;
                        timesheet.employee = employee.Data.employee;
                        timesheet.company = employee.Data.company;
                        timesheet.department = employee.Data.department;
                        timesheet.time_logs = TimeSheetLogs;
                        timesheet.docstatus = 1;
                        //timesheet.total_hours = workinghours; 
                        Client.InsertObject(timesheet.Object);
                        Log.Info($"created time sheet for the employee {empid} successfully");
                    }

                    //finally update Employee with lastattendance date
                    ERPObject updated_obj = new ERPObject(DocType.Employee);
                    updated_obj.Data.last_attendance_date = ProcessingDate.ToString("yyyy-MM-dd");
                    Client.UpdateObject(DocType.Employee, empid, updated_obj);
                    Log.Info($"updated the processed date for the employee {empid} successfully");
                }
            }

            catch (Exception ex)
            {

                Log.Error(ex);
            }
        }
    }

}


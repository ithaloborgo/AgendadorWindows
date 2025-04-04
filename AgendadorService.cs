using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using TimersTimer = System.Timers.Timer;
using System.Timers;

namespace Agendador
{
    public partial class AgendadorService : ServiceBase
    {
        public AgendadorService()
        {
            ServiceName = "AgendadorService";
            // Removemos a configuração do EventLog, pois não precisamos de logs.
        }

        // Métodos auxiliares para testes (modo console)
        public void StartService(string[] args)
        {
            this.OnStart(args);
        }

        public void StopService()
        {
            this.OnStop();
        }

        // Caminho do arquivo INI (na mesma pasta do executável)
        private string iniFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
        // Lista de tarefas configuradas
        private List<TaskInfo> tasks = new List<TaskInfo>();
        // Timer para verificação
        private TimersTimer timer;

        // Importa a função da API para ler o arquivo INI
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern int GetPrivateProfileString(
            string section, string key, string defaultValue,
            System.Text.StringBuilder retVal, int size, string filePath);

        // Classe para armazenar as informações de cada tarefa
        public class TaskInfo
        {
            public string FileToOpen { get; set; }
            public string Days { get; set; }
            public string TimeToExecute { get; set; }
            public int IntervalInMinutes { get; set; }
            public DateTime? LastExecuted { get; set; }
        }

        protected override void OnStart(string[] args)
        {
            InitializeTasks();

            // Se nenhuma tarefa for configurada corretamente, não é feito log.
            // Caso queira, você pode usar Console.WriteLine() em modo interativo.

            timer = new TimersTimer(1000);
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        protected override void OnStop()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
            }
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            DateTime now = DateTime.Now;

            foreach (var task in tasks)
            {
                var daysList = task.Days.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                bool isScheduledDay = false;
                foreach (var day in daysList)
                {
                    if (now.DayOfWeek.ToString().Equals(day.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        isScheduledDay = true;
                        break;
                    }
                }
                if (!isScheduledDay)
                    continue;

                if (!TimeSpan.TryParse(task.TimeToExecute, out TimeSpan scheduleTime))
                {
                    continue;
                }

                DateTime scheduledDateTime = now.Date + scheduleTime;
                TimeSpan tolerancia = TimeSpan.FromMinutes(1);

                bool shouldRun = false;
                bool isWithinScheduleWindow = now >= scheduledDateTime && (now - scheduledDateTime).TotalMinutes <= 1;

                if (task.LastExecuted == null)
                {
                    shouldRun = isWithinScheduleWindow;
                }
                else if (task.IntervalInMinutes > 0)
                {
                    TimeSpan elapsed = now - task.LastExecuted.Value;
                    shouldRun = elapsed.TotalMinutes >= task.IntervalInMinutes;
                }
                else
                {
                    shouldRun = task.LastExecuted.Value.Date != now.Date && isWithinScheduleWindow;
                }

                if (shouldRun)
                    {
                        try
                        {
                            Process.Start(task.FileToOpen);
                            task.LastExecuted = now;
                        }
                        catch (Exception)
                        {
                            // Tratar exceção
                        }
                    }
                
            }
        }

        private void InitializeTasks()
        {
            if (!File.Exists(iniFilePath))
            {
                string defaultContent =
@"[Task1]
FileToOpen=C:\Caminho\Para\Programa1.exe
Days=Monday,Tuesday,Wednesday,Thursday,Friday
Time=12:00
Interval=60

[Task2]
FileToOpen=C:\Caminho\Para\Programa2.exe
Days=Saturday,Sunday
Time=15:00
Interval=120

[Task3]
FileToOpen = C:\Caminho\Para\Programa3.exe
Days = Saturday, Sunday
Time = 15:00
Interval = 120";
                File.WriteAllText(iniFilePath, defaultContent);
            }

            string[] tasksSections = { "Task1", "Task2", "Task3" };

            foreach (var section in tasksSections)
            {
                string fileToOpen = ReadIni(section, "FileToOpen", "");
                string days = ReadIni(section, "Days", "");
                string timeToExecute = ReadIni(section, "Time", "");
                string intervalStr = ReadIni(section, "Interval", "0");
                int.TryParse(intervalStr, out int interval);

                if (string.IsNullOrEmpty(fileToOpen) || string.IsNullOrEmpty(days) || string.IsNullOrEmpty(timeToExecute))
                {
                    continue;
                }

                tasks.Add(new TaskInfo
                {
                    FileToOpen = fileToOpen,
                    Days = days,
                    TimeToExecute = timeToExecute,
                    IntervalInMinutes = interval,
                    LastExecuted = null
                });
            }
        }

        private string ReadIni(string section, string key, string defaultValue)
        {
            var retVal = new System.Text.StringBuilder(255);
            GetPrivateProfileString(section, key, defaultValue, retVal, 255, iniFilePath);
            return retVal.ToString();
        }
    }
}

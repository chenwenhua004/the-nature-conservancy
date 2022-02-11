using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TestProject1
{
	class Logger
	{
		private string logFilePath;
		private StreamWriter outputFile;

		public Logger() 
		{
			logFilePath = Directory.GetCurrentDirectory() + "\\" + "log.txt";
			outputFile = new StreamWriter(logFilePath);
			outputFile.AutoFlush = true;
		}

		public void Trace(string message)
		{
			outputFile.WriteLine(message);

			//using (StreamWriter outputFile = new StreamWriter(logFilePath))
			//{
				//outputFile.WriteLine(message);
			//}

			Console.WriteLine("writed the log to the log file!");
		}
	}
}

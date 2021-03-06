﻿using Microsoft.AspNet.Identity;
using PostSys.Models;
using PostSys.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using System.Configuration;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.IO;
using Ionic.Zip;

namespace PostSys.Controllers
{
	public class PostsController : Controller
	{
		private ApplicationDbContext _context;

		public PostsController()
		{
			_context = new ApplicationDbContext();
		}
		
		public ActionResult Index()
		{
			var showPost = _context.Posts.Include(m => m.Course).ToList();
			var showCourse = _context.Courses.Include(m => m.Class).Include(m => m.Student).ToList();

			return View(showPost);
		}


		[HttpGet]
		public ActionResult MinePost()
		{
			var getCurrentStudent = User.Identity.GetUserId();
			var obj = (from c in _context.Courses
					   where c.StudentId.Contains(getCurrentStudent)
					   join p in _context.Posts
					   on c.Id equals p.CourseId
					   select new
					   {
						   Id = p.Id, 
						   Name = p.Name,
						   Course = c.Name
					   }).ToList().Select(po => new PostCourseViewModel()
					   {
						   postName = po.Name,
						   postId = po.Id,
						   courseName  = po.Course
					   }
					   ).ToList();
			return View(obj);
		}

		[HttpGet]
		public ActionResult Delete(int Id)
		{
			var courseInDb = _context.Posts.SingleOrDefault(c => c.Id == Id);

			_context.Posts.Remove(courseInDb);
			_context.SaveChanges();

			return RedirectToAction("Index");
		}

		[HttpGet]
		public ActionResult DeleteMinePost (int Id)
		{
			var courseInDb = _context.Posts.SingleOrDefault(c => c.Id == Id);

			_context.Posts.Remove(courseInDb);
			_context.SaveChanges();

			return RedirectToAction("MinePost");
		}



		[HttpGet]
		public ActionResult ManagePost()
		{
			{
				//Get coor ID
				var currentCoorId = User.Identity.GetUserId();
				var currentCoorClass = (from cl in _context.Classes where cl.CoordinatorId == currentCoorId select cl.Id).ToList();
				var currentCoorClassId = currentCoorClass[0];

				var ojb = (from c in _context.Courses
						   where c.ClassId == currentCoorClassId
						   join p in _context.Posts
						   on c.Id equals p.CourseId
						   select new
						   {
							   postId = p.Id,
							   postName = p.Name,
							   courseName = c.Name,
							   studentName = (from st in _context.Users where st.Id == c.StudentId select st.UserName),
							   className = (from cl in _context.Classes where cl.Id == c.ClassId select cl.Name),
						   }
					).ToList().Select(p => new PostCourseViewModel()
					{
						postId = p.postId,
						postName = p.postName,
						courseName = p.courseName,
						studentName = string.Join(",", p.studentName),
						className = string.Join(",", p.className)
					}
					);

				return View(ojb);
			}
		}

		public FileResult Download(Post post)
		{

			var getFileById = _context.Posts.SingleOrDefault(c => c.Id == post.Id);

			return File(getFileById.File, "file", getFileById.UrlFile);
		}


		[HttpGet]
		public ActionResult AddComment()
		{
			return View();
		}

		[HttpPost]
		public ActionResult AddComment(Post post, Comment comment)
		{
			var getPostId = _context.Posts.SingleOrDefault(m => m.Id == post.Id);

			var newComent = new Comment
			{
				PostId = getPostId.Id,
				Reply = comment.Reply
			};

			_context.Comments.Add(newComent);
			_context.SaveChanges();
			
			return View("~/Views/Home/Index.cshtml");
		}

		[HttpGet]
		public ActionResult ShowComment(Post post, int id)
		{
			var getPostId = _context.Posts.SingleOrDefault(m => m.Id == post.Id);

			var getCommentInPost = _context.Comments.Where(m => m.PostId == id).ToList();

			return View(getCommentInPost);
		}

		public bool SendEmail(string toEmail, string emailSubject, string emailBody)
		{

			var senderEmail = ConfigurationManager.AppSettings["SenderEmail"].ToString();
			var senderPassword = ConfigurationManager.AppSettings["SenderPassword"].ToString();

			SmtpClient smtpClient = new SmtpClient("smtp.gmail.com", 587);
			smtpClient.EnableSsl = true;
			smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
			smtpClient.UseDefaultCredentials = false;
			smtpClient.Credentials = new NetworkCredential(senderEmail, senderPassword);

			MailMessage mailMessage = new MailMessage(senderEmail, toEmail, emailSubject, emailBody);
			mailMessage.IsBodyHtml = true;
			mailMessage.BodyEncoding = UTF8Encoding.UTF8;

			smtpClient.Send(mailMessage);

			return true;

		}

		public ActionResult SendEmailToUser()
		{
			//get coordinator email
			bool result = false;
			var getCurrentStudent = User.Identity.GetUserName();
			

			var courseStudent = (from st in _context.Users
								 where st.UserName.Contains(getCurrentStudent)
								 join c in _context.Courses
								 on st.Id equals c.StudentId
								 select c.ClassId).ToList();
			var courseStudentId = courseStudent[0];
			var classCoordinator = (from cc in _context.Classes
									where cc.Id == courseStudentId
									join co in _context.Users
									on cc.CoordinatorId equals co.Id
									select co.Email).ToList();
			var coordinatorEmail = classCoordinator[0];

			//get Course Name
			var getCurrentStudentId = User.Identity.GetUserId();
			var currentCourseNames = (from co in _context.Courses where co.StudentId.Contains(getCurrentStudentId) select co.Name).ToList();
			var currentCourseName = currentCourseNames[0];

			//get Class Name
			var classNames = (from c in _context.Courses
							  where c.StudentId.Contains(getCurrentStudentId)
							  join cl in _context.Classes
							  on c.ClassId equals cl.Id
							  select cl.Name).ToList();
			var className = classNames[0];


			result = SendEmail($"{coordinatorEmail}", "Notification Email", $"Student: {getCurrentStudent} <br> Course: {currentCourseName} <br> Class: {className} <br> Already submit a post");


			return Json(result, JsonRequestBehavior.AllowGet);
		}



		/////////////////////////////////////////
		//Only Manager can see this index
		/*[HttpGet]
		public ActionResult ManagerIndex()
		{
			string[] filePaths = Directory.GetFiles(Server.MapPath("~/Files/"));
			List<FileModel> files = new List<FileModel>();
			foreach (string filePath in filePaths)
			{
				files.Add(new FileModel()
				{
					FileName = Path.GetFileName(filePath),
					FilePath = filePath
				});
			}

			return View(files);
		}

		[HttpPost]
		public ActionResult ManagerIndex(List<FileModel> files)
		{
			using (ZipFile zip = new ZipFile())
			{
				zip.AlternateEncodingUsage = ZipOption.AsNecessary;
				zip.AddDirectoryByName("Files");
				foreach (FileModel file in files)
				{
					if (file.IsSelected)
					{
						zip.AddFile(file.FilePath, "Files");
					}
				}
				string zipName = String.Format("FilesZip_{0}.zip", DateTime.Now.ToString("yyyy-MMM-dd-HHmmss"));
				using (MemoryStream memoryStream = new MemoryStream())
				{
					zip.Save(memoryStream);
					return File(memoryStream.ToArray(), "application/zip", zipName);
				}
			}
		}*/
	}
}


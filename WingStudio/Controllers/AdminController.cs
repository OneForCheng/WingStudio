﻿using PagedList;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using WingStudio.Models;

namespace WingStudio.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseController
    {
        private User Loginer => Entity.Users.Find(Convert.ToInt32(User.Identity.Name));

        /// <summary>
        /// 页面导航
        /// </summary>
        /// <returns></returns>
        public ActionResult Dashboard()
        {
            var logined = Loginer;
            if (logined.Groups.Count(m => (m.Authority & (int)AuthorityFlag.ManageNotice) != 0) > 0)
            {
                return RedirectToAction("ManageNotice");
            }
            if (logined.Groups.Count(m => (m.Authority & (int)AuthorityFlag.ManageDynamic) != 0) > 0)
            {
                return RedirectToAction("ManageDynamic");
            }
            if (logined.Groups.Count(m => (m.Authority & (int)AuthorityFlag.ManageBlog) != 0) > 0)
            {
                return RedirectToAction("ManageBlog");
            }
            if (logined.Groups.Count(m => (m.Authority & (int)AuthorityFlag.ManageFile) != 0) > 0)
            {
                return RedirectToAction("ManageFile");
            }
            if (logined.Groups.Count(m => (m.Authority & (int)AuthorityFlag.ManageApplication) != 0) > 0)
            {
                return RedirectToAction("ManageApplication");
            }
            if (logined.Groups.Count(m => (m.Authority & (int)AuthorityFlag.ManageMessage) != 0) > 0)
            {
                return RedirectToAction("ManageReceivedMsg");
            }
            return View();
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="emailInfo"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult SendEmail(EmailInfo emailInfo)
        {
            if (WebSecurity.IsValidEmailInfo(emailInfo))
            {
                if (WebHelper.SendMail(emailInfo.ToEmail, emailInfo.Theme, emailInfo.Content))
                {
                    return Json(new { state = "发送成功", email = emailInfo.ToEmail });
                }
                else
                {
                    return Json(new { state = "发送超时", email = emailInfo.ToEmail });
                }
            }
            else
            {
                return Json(new { state = "发送失败", email = emailInfo.ToEmail });
            }
        }

        /// <summary>
        /// 上传图标
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public ActionResult UploadIcon()
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageDynamic | AuthorityFlag.ManageFile | AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }

            if (Request.Files.Count == 0)
            {
                return Json(new { status = "error", message = "上传图片不能为空!" });
            }
            var picture = Request.Files[0];
            if (picture.ContentLength > 5242880)
            {
                return Json(new { status = "error", message = "上传图片大小超出指定范围(5M)!" });
            }
            var fileName = picture.FileName.Split('\\').Last();
            if (!fileName.Contains("."))
            {
                return Json(new { status = "error", message = "上传图片格式不正确(.jpg/.png/.gif)!" });
            }
            var fileExt = fileName.Substring(fileName.LastIndexOf('.')).ToLower();
            if (fileExt == ".jpg" || fileExt == ".png" || fileExt == ".gif")
            {
                var path = Server.MapPath("~/Content/Images/Shared/Icon");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                fileName = DateTime.Now.ToFileTime().ToString() + Guid.NewGuid().ToString("N") + fileExt;
                var picPath = Path.Combine(path, fileName);
                picture.SaveAs(picPath);//从客户端保存文件到本地

                var image = new WebImage(picPath);
                double height = image.Height;
                double width = image.Width;
                return Json(new
                {
                    status = "success",
                    url = $"/Content/Images/Shared/Icon/{fileName}", width, height
                });
            }
            else
            {
                return Json(new { status = "error", message = "上传图片格式不正确(.jpg/.png/.gif)!" });
            }
        }

        /// <summary>
        /// 剪切图标
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public ActionResult CropIcon(CropPicture picture, int id, string type)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageDynamic | AuthorityFlag.ManageFile | AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            dynamic one;
            switch(type)
            {
                case "dynamic":
                    one = Entity.Dynamics.Find(id);
                    break;
                case "blogGroup":
                    one = Entity.BlogGroups.Find(id);
                    break;
                case "fileGroup":
                    one = Entity.FileGroups.Find(id);
                    break;
                default:
                    one = Entity.Dynamics.Find(id);
                    break;
            }
            if(one == null)
            {
                return View("Error");
            }
            try
            {
                var picPath = Server.MapPath(picture.imgUrl);
                if (System.IO.File.Exists(picPath))
                {
                    var image = new WebImage(picPath);
                    image.Resize((int)picture.imgW, (int)picture.imgH);

                    image.Crop((int)picture.imgY1, (int)picture.imgX1, (int)(picture.imgH - picture.cropH - picture.imgY1), (int)(picture.imgW - picture.cropW - picture.imgX1));
                    image.Save(picPath);

                    var fileName = picPath.Split('\\').Last();
                    one.Icon = fileName;
                    Entity.SaveChanges();
                    return Json(new { status = "success", url = $"/Content/Images/Shared/Icon/{fileName}" });
                }
                else
                {
                    return Json(new { status = "error", message = "剪切失败，找不到目标图片!" });
                }
            }
            catch(Exception)
            {
                return Json(new { status = "error", message = "操作异常，图片剪切失败!" });
            }

        }

        #region 公告管理

        /// <summary>
        /// 公告管理
        /// </summary>
        /// <param name="page">分页</param>
        /// <returns></returns>
        public ActionResult ManageNotice(int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageNotice))
            {
                return View("Error");
            }
            ViewBag.IsSearch = false;
            var notices = Entity.Notices.OrderByDescending(m => m.Id);
            var pageNumber = page ?? 1;
            ViewBag.PageNumber = pageNumber;
            var onePageOfProducts = notices.ToPagedList(pageNumber, 10);
            return View("ManageNotice", onePageOfProducts);
        }

        /// <summary>
        /// 添加公告
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult AddNotice()
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageNotice))
            {
                return View("Error");
            }
            ViewBag.IsModified = false;
            return View("SaveNotice");
        }

        /// <summary>
        /// 添加公告
        /// </summary>
        /// <param name="notice"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddNotice(Notice notice)
        {
          
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageNotice))
            {
                return View("Error");
            }
            //检查公告信息是否合法
            if ( WebSecurity.IsValidNotice(notice))
            {
                var newNotice = new Notice
                {
                    Publisher = logined,
                    Theme = notice.Theme.Trim(),
                    Content = notice.Content,
                    IsPublic = notice.IsPublic
                };
                if(notice.IsPublic)
                {
                    newNotice.PublicTime = DateTime.Now;
                }
                newNotice.IsLong = notice.IsLong;
                Entity.Notices.Add(newNotice);
                Entity.SaveChanges();
                InfoLog.Info($"Envent:[添加公告事件]  AddNotice[Theme:{newNotice.Theme} Id:{newNotice.Id}] Operator[Role:Admin Account:{logined.Account} Id:{logined.Id}]");
                return Content(WebHelper.SweetAlert("添加成功", "成功添加公告!", "location.href='/Admin/ManageNotice'"));
            }
            else
            {
                return Content(WebHelper.SweetAlert("添加失败", "提交的信息不合法!"));
            }
        }

        /// <summary>
        /// 修改公告
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult ModNotice(int id)
        {

            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageNotice))
            {
                return View("Error");
            }
            var notice = Entity.Notices.Find(id);
            if (notice != null)
            {
                ViewBag.IsModified = true;
                return View("SaveNotice", notice);
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 修改公告
        /// </summary>
        /// <param name="id"></param>
        /// <param name="notice"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ModNotice(int id, Notice notice)
        {

            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageNotice))
            {
                return View("Error");
            }
            //检查公告信息是否合法
            var targetNotice = Entity.Notices.Find(id);
            if (targetNotice != null &&  WebSecurity.IsValidNotice(notice))
            {
                targetNotice.LastModTime = DateTime.Now;
                targetNotice.Publisher = logined;
                targetNotice.Theme = notice.Theme.Trim();
                targetNotice.Content = notice.Content;
                if(targetNotice.IsPublic != notice.IsPublic)
                {
                    targetNotice.IsPublic = notice.IsPublic;
                    if(notice.IsPublic)
                    {
                        targetNotice.PublicTime = DateTime.Now;
                    }
                    else
                    {
                        targetNotice.PublicTime = null;
                    }
                }

                targetNotice.IsLong = notice.IsLong;
                Entity.SaveChanges();
                InfoLog.Info($"Envent:[修改公告事件] ModNotice[Theme:{targetNotice.Theme} Id:{targetNotice.Id}] Operator[Role:Admin Account:{logined.Account} Id:{logined.Id}]");
                return Content(WebHelper.SweetAlert("修改成功", "成功修改公告!", "location.href='/Admin/ManageNotice'"));
            }
            else
            {
                return Content(WebHelper.SweetAlert("修改失败", "提交的信息不合法!"));
            }
        }

        /// <summary>
        /// 删除公告
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult DelNotice(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageNotice))
            {
                return View("Error");
            }
            var notice = Entity.Notices.Find(id);
            if (notice != null)
            {
                //notice.Publisher = null;
                Entity.Notices.Remove(notice);
                Entity.SaveChanges();
                InfoLog.Info($"Envent:[删除公告事件] DelNotice[Theme:{notice.Theme} Id:{notice.Id}] Operator[Role:Admin Account:{logined.Account} Id:{logined.Id}]");
                return Content(WebHelper.SweetAlert("删除成功", "成功删除公告!"));
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 搜索公告
        /// </summary>
        /// <param name="searchContent"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult SearchNotice(string searchContent, int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageNotice))
            {
                return View("Error");
            }
            ViewBag.IsSearch = true;
            if (string.IsNullOrWhiteSpace(searchContent))
            {
                return View("ManageNotice", new List<Notice>().ToPagedList(1, 10));
            }
            else
            {
                searchContent = searchContent.Trim();
                var notices = Entity.Notices.Where(m => m.Theme.Contains(searchContent)).OrderByDescending(m => m.Id);
                var pageNumber = page ?? 1;
                ViewBag.PageNumber = pageNumber;
                var onePageOfProducts = notices.ToPagedList(pageNumber, 10);
                ViewBag.SearchContent = searchContent;
                return View("ManageNotice", onePageOfProducts);
            }
        }

        #endregion

        #region 动态管理

        /// <summary>
        /// 管理动态
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult ManageDynamic(int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageDynamic))
            {
                return View("Error");
            }
            ViewBag.IsSearch = false;
            var dynamics = Entity.Dynamics.OrderByDescending(m => m.Id);
            var pageNumber = page ?? 1;
            ViewBag.PageNumber = pageNumber;
            var onePageOfProducts = dynamics.ToPagedList(pageNumber, 10);
            return View("ManageDynamic", onePageOfProducts);
        }

        /// <summary>
        /// 添加动态
        /// </summary>
        /// <returns></returns>
        [HttpGet] 
        public ActionResult AddDynamic()
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageDynamic))
            {
                return View("Error");
            }
            ViewBag.IsModified = false;
            return View("SaveDynamic");
        }

        /// <summary>
        /// 添加动态
        /// </summary>
        /// <param name="dynamic"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddDynamic(Dynamic dynamic)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageDynamic))
            {
                return View("Error");
            }
            //检查动态信息是否合法
            if ( WebSecurity.IsValidDynamic(dynamic))
            {
                var newDynamic = new Dynamic
                {
                    Publisher = logined,
                    Theme = dynamic.Theme.Trim(),
                    Content = dynamic.Content,
                    IsPublic = dynamic.IsPublic
                };
                if(dynamic.IsPublic)
                {
                    newDynamic.PublicTime = DateTime.Now;
                }
                newDynamic.IsFormal = dynamic.IsFormal;
                Entity.Dynamics.Add(newDynamic);
                Entity.SaveChanges();
                InfoLog.Info($"Envent:[添加动态事件] AddDynamic[Theme:{newDynamic.Theme} Id:{newDynamic.Id}] Operator[Role:Admin Account:{logined.Account} Id:{logined.Id}]");
                return Content(WebHelper.SweetAlert("添加成功", "成功添加动态!", "location.href='/Admin/ManageDynamic'"));
            }
            else
            {
                return Content(WebHelper.SweetAlert("添加失败", "提交的信息不合法!"));
            }
        }

        /// <summary>
        /// 修改动态
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult ModDynamic(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageDynamic))
            {
                return View("Error");
            }
            var dynamic = Entity.Dynamics.Find(id);
            if (dynamic != null)
            {
                ViewBag.IsModified = true;
                return View("SaveDynamic", dynamic);
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 修改动态
        /// </summary>
        /// <param name="id"></param>
        /// <param name="dynamic"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ModDynamic(int id, Dynamic dynamic)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageDynamic))
            {
                return View("Error");
            }
            //检查动态信息是否合法
            var targetDynamic = Entity.Dynamics.Find(id);
            if (targetDynamic != null &&  WebSecurity.IsValidDynamic(dynamic))
            {
                targetDynamic.Publisher = logined;
                targetDynamic.Theme = dynamic.Theme.Trim();
                targetDynamic.Content = dynamic.Content;
                if(targetDynamic.IsPublic != dynamic.IsPublic)
                {
                    targetDynamic.IsPublic = dynamic.IsPublic;
                    if(dynamic.IsPublic)
                    {
                        targetDynamic.PublicTime = DateTime.Now;
                    }
                    else
                    {
                        targetDynamic.PublicTime = null;
                    }
                }
                targetDynamic.IsFormal = dynamic.IsFormal;
                Entity.SaveChanges();
                InfoLog.Info($"Envent:[修改动态事件] ModDynamic[Theme:{targetDynamic.Theme} Id:{targetDynamic.Id}] Operator[Role:Admin Account:{logined.Account} Id:{logined.Id}]");
                return Content(WebHelper.SweetAlert("修改成功", "成功修改动态!", "location.href='/Admin/ManageDynamic'"));
            }
            else
            {
                return Content(WebHelper.SweetAlert("修改失败", "提交的信息不合法!"));
            }
        }

        /// <summary>
        /// 删除动态
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult DelDynamic(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageDynamic))
            {
                return View("Error");
            }
            var dynamic = Entity.Dynamics.Find(id);
            if (dynamic != null)
            {
                Entity.Dynamics.Remove(dynamic);
                Entity.SaveChanges();
                InfoLog.Info($"Envent:[删除动态事件] DelDynamic[Theme:{dynamic.Theme} Id:{dynamic.Id}] Operator[Role:Admin Account:{logined.Account} Id:{logined.Id}]");
                return Content(WebHelper.SweetAlert("删除成功", "成功删除动态!"));
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 搜索动态
        /// </summary>
        /// <param name="searchContent"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult SearchDynamic(string searchContent, int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageDynamic))
            {
                return View("Error");
            }
            ViewBag.IsSearch = true;
            if (string.IsNullOrWhiteSpace(searchContent))
            {
                return View("ManageDynamic", new List<Dynamic>().ToPagedList(1, 10));
            }
            else
            {
                searchContent = searchContent.Trim();
                var dynamics = Entity.Dynamics.Where(m => m.Theme.Contains(searchContent)).OrderByDescending(m => m.Id);
                var pageNumber = page ?? 1;
                ViewBag.PageNumber = pageNumber;
                var onePageOfProducts = dynamics.ToPagedList(pageNumber, 10);
                ViewBag.SearchContent = searchContent;
                return View("ManageDynamic", onePageOfProducts);
            }
        }
        #endregion

        #region 博客管理

        #region 博客组
        /// <summary>
        /// 管理博客组
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult ManageBlogGroup(int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            ViewBag.IsSearch = false;
            var blogGroups = Entity.BlogGroups.OrderByDescending(m => m.Id);
            var pageNumber = page ?? 1;
            ViewBag.PageNumber = pageNumber;
            var onePageOfProducts = blogGroups.ToPagedList(pageNumber, 10);
            return View("ManageBlogGroup", onePageOfProducts);
        }

        /// <summary>
        /// 添加博客组
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult AddBlogGroup()
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            ViewBag.IsModified = false;
            return View("SaveBlogGroup");
        }

        /// <summary>
        /// 添加博客组
        /// </summary>
        /// <param name="blogGroup"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult AddBlogGroup(BlogGroup blogGroup)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            //检查博客组信息是否合法
            if (WebSecurity.IsValidBlogGroup(blogGroup))
            {
                var newBlogGroup = new BlogGroup
                {
                    Theme = blogGroup.Theme.Trim(),
                    Accessible = blogGroup.Accessible,
                    Description = blogGroup.Description.Trim()
                };
                Entity.BlogGroups.Add(newBlogGroup);
                Entity.SaveChanges();

                return Content(WebHelper.SweetAlert("添加成功", "成功添加博客组!", "location.href='/Admin/ManageBlogGroup'"));
            }
            else
            {
                return Content(WebHelper.SweetAlert("添加失败", "提交的信息不合法!"));
            }
        }

        /// <summary>
        /// 修改博客组
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult ModBlogGroup(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            var blogGroup = Entity.BlogGroups.Find(id);
            if (blogGroup != null)
            {
                ViewBag.IsModified = true;
                return View("SaveBlogGroup", blogGroup);
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 修改博客组
        /// </summary>
        /// <param name="id"></param>
        /// <param name="blogGroup"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult ModBlogGroup(int id, BlogGroup blogGroup)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            //检查博客组信息是否合法
            var targetGroup = Entity.BlogGroups.Find(id);
            if (targetGroup != null && WebSecurity.IsValidBlogGroup(blogGroup))
            {
                targetGroup.Theme = blogGroup.Theme.Trim();
                targetGroup.Accessible = blogGroup.Accessible;
                targetGroup.Description = blogGroup.Description.Trim();
                Entity.SaveChanges();

                return Content(WebHelper.SweetAlert("修改成功", "成功修改博客组!", "location.href='/Admin/ManageBlogGroup'"));
            }
            else
            {
                return Content(WebHelper.SweetAlert("修改失败", "提交的信息不合法!"));
            }
        }

      
        /// <summary>
        /// 删除博客组
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult DelBlogGroup(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            var blogGroup = Entity.BlogGroups.Find(id);
            if (blogGroup != null)
            {
                Entity.BlogGroups.Remove(blogGroup);
                Entity.SaveChanges();
                return Content(WebHelper.SweetAlert("删除成功", "成功删除知识专栏组!"));
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 显示博客组中博客
        /// </summary>
        /// <param name="id"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult ShowGroupBlogs(int id, int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            var blogGroup = Entity.BlogGroups.Find(id);
            if (blogGroup != null)
            {
                ViewBag.GroupId = id;
                ViewBag.GroupName = blogGroup.Theme;
                var blogs = blogGroup.Blogs;
                var pageNumber = page ?? 1;
                ViewBag.PageNumber = pageNumber;
                var onePageOfProducts = blogs.ToPagedList(pageNumber, 10);
                return View(onePageOfProducts);
            }
            else
            {
                return View("Error");
            }
        }

        
        #endregion

        #region 博客

        /// <summary>
        /// 已检查博客
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult ManageBlog(int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            ViewBag.IsSearch = false;
            var blogs = Entity.Blogs.Where(m => m.Checked && m.IsPublic).OrderByDescending(m => m.PublicTime);
            var pageNumber = page ?? 1;
            ViewBag.PageNumber = pageNumber;
            var onePageOfProducts = blogs.ToPagedList(pageNumber, 10);
            return View("ManageBlog", onePageOfProducts);
        }

        /// <summary>
        /// 待检测博客
        /// </summary>
        /// <returns></returns>
        public ActionResult CheckingBlog(int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            var blogs = Entity.Blogs.Where(m => !m.Checked && m.IsPublic).OrderByDescending(m => m.PublicTime);
            var pageNumber = page ?? 1;
            ViewBag.PageNumber = pageNumber;
            var onePageOfProducts = blogs.ToPagedList(pageNumber, 10);
            return View("CheckingBlog", onePageOfProducts);
        }

        /// <summary>
        /// 博客通过检查
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult BlogPassCheck(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            var blog = Entity.Blogs.Find(id);
            if (blog != null && !blog.Checked && blog.IsPublic)
            {
                blog.Checked = true;
                blog.CheckId = logined.Id;
                Entity.SaveChanges();
                if (Entity.Blogs.Count(m => !m.Checked && m.IsPublic) > 0)
                {
                    return RedirectToAction("CheckingBlog");
                }
                else
                {
                    return RedirectToAction("ManageBlog");
                }
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 显示博客
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult ShowBlog(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            var blog = Entity.Blogs.Find(id);
            if (blog != null && blog.IsPublic)
            {
                blog.LookCount++;
                Entity.SaveChanges();
                var blogs = Entity.Blogs.Where(m => m.Owner.Id == blog.Owner.Id && m.IsPublic);
                ViewBag.LastBlog = blogs.Where(m => m.Id < id).OrderByDescending(m => m.Id).FirstOrDefault();
                ViewBag.NextBlog = blogs.Where(m => m.Id > id).OrderBy(m => m.Id).FirstOrDefault();
                return View("ShowBlog", blog);
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 搜索博客
        /// </summary>
        /// <param name="searchContent"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult SearchBlog(string searchContent, int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            ViewBag.IsSearch = true;
            if (string.IsNullOrWhiteSpace(searchContent))
            {
                return View("ManageBlog", new List<Blog>().ToPagedList(1, 10));
            }
            else
            {
                searchContent = searchContent.Trim();
                var blogs = Entity.Blogs.Where(m => m.Checked && m.IsPublic).Where(m => m.Theme.Contains(searchContent)).OrderByDescending(m => m.Id);
                var pageNumber = page ?? 1;
                ViewBag.PageNumber = pageNumber;
                var onePageOfProducts = blogs.ToPagedList(pageNumber, 10);
                ViewBag.SearchContent = searchContent;
                return View("ManageBlog", onePageOfProducts);
            }
        }

        #endregion

        /// <summary>
        /// 将博客添加到指定博客组中
        /// </summary>
        /// <param name="id"></param>
        /// <param name="groupId"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult AddBlogToGroup(int id, int groupId)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            var blog = Entity.Blogs.Find(id);
            var blogGroup = Entity.BlogGroups.Find(groupId);
            if (blog != null && blog.Checked && blog.IsPublic && blogGroup != null)
            {
                blogGroup.Blogs.Add(blog);
                Entity.SaveChanges();
                return Json(new { title = "添加成功", message = "成功添加指定博客到指定组!" });
            }
            else
            {
                return Json(new { title = "添加失败", message = "提交信息不合法!" });
            }
        }

        /// <summary>
        /// 返回指定组能够添加的博客信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult EnableAddBlogs(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            var group = Entity.BlogGroups.Find(id);
            if (group != null)
            {
                var blogInfos = Entity.Blogs.Where(m => m.Checked && m.IsPublic).ToList().Except(group.Blogs).Select(m => new { m.Id, m.Theme, UserAccount = m.Owner.Account, UserName = m.Owner.Name }).OrderByDescending(m =>  m.Id);
                if (blogInfos.Any())
                {
                    return Json((new JavaScriptSerializer()).Serialize(blogInfos));
                }
                else
                {
                    return Json(new { });
                }
            }
            else
            {
                return Json(new { });
            }
        }

        /// <summary>
        /// 返回指定博客能够添加的所有组信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult EnableAddBlogGroups(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            var blog = Entity.Blogs.Find(id);
            if (blog != null)
            {
                var groupInfos = Entity.BlogGroups.ToList().Except(blog.Groups).Select(m => new { m.Id, m.Theme, m.Description }).OrderByDescending(m => m.Id);
                if (groupInfos.Any())
                {
                    return Json((new JavaScriptSerializer()).Serialize(groupInfos));
                }
                else
                {
                    return Json(new { });
                }
            }
            else
            {
                return Json(new { });
            }
        }

        /// <summary>
        /// 返回指定博客所属组信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult EnableDelBlogGroups(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            var blog = Entity.Blogs.Find(id);
            if (blog != null)
            {
                var groupInfos = blog.Groups.Select(m => new { m.Id, m.Theme, m.Description }).OrderByDescending(m => m.Id);
                if (groupInfos.Any())
                {
                    return Json((new JavaScriptSerializer()).Serialize(groupInfos));
                }
                else
                {
                    return Json(new { });
                }
            }
            else
            {
                return Json(new { });
            }
        }

        /// <summary>
        /// 从指定博客组中移除指定博客
        /// </summary>
        /// <param name="id"></param>
        /// <param name="groupId"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult DelBlogFromGroup(int id, int groupId)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            var blog = Entity.Blogs.Find(id);
            var blogGroup = Entity.BlogGroups.Find(groupId);
            if (blog != null && blogGroup != null && blogGroup.Blogs.Count(m => m.Id == id) > 0)
            {
                blogGroup.Blogs.Remove(blog);
                Entity.SaveChanges();
                return Json(new { title = "删除成功", message = "成功从指定组删除指定博客!" });
            }
            else
            {
                return Json(new { title = "删除失败", message = "提交信息不合法!" });
            }
        }

        #endregion

        #region 文件管理

        #region 文件组
        /// <summary>
        /// 管理文件组
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult ManageFileGroup(int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            ViewBag.IsSearch = false;
            var fileGroups = Entity.FileGroups.OrderByDescending(m => m.Id);
            var pageNumber = page ?? 1;
            ViewBag.PageNumber = pageNumber;
            var onePageOfProducts = fileGroups.ToPagedList(pageNumber, 10);
            return View("ManageFileGroup", onePageOfProducts);
        }

        /// <summary>
        /// 添加文件组
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult AddFileGroup()
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            ViewBag.IsModified = false;
            return View("SaveFileGroup");
        }

        /// <summary>
        /// 添加文件组
        /// </summary>
        /// <param name="fileGroup"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult AddFileGroup(FileGroup fileGroup)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            //检查文件组信息是否合法
            if (WebSecurity.IsValidFileGroup(fileGroup))
            {
                var newFileGroup = new FileGroup
                {
                    Accessible = fileGroup.Accessible,
                    Theme = fileGroup.Theme.Trim(),
                    Description = fileGroup.Description.Trim()
                };
                Entity.FileGroups.Add(newFileGroup);
                Entity.SaveChanges();

                return Content(WebHelper.SweetAlert("添加成功", "成功添加文件组!", "location.href='/Admin/ManageFileGroup'"));
            }
            else
            {
                return Content(WebHelper.SweetAlert("添加失败", "提交的信息不合法!"));
            }
        }

        /// <summary>
        /// 修改文件组
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult ModFileGroup(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            var fileGroup = Entity.FileGroups.Find(id);
            if (fileGroup != null)
            {
                ViewBag.IsModified = true;
                return View("SaveFileGroup", fileGroup);
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 修改文件组
        /// </summary>
        /// <param name="id"></param>
        /// <param name="fileGroup"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult ModFileGroup(int id, FileGroup fileGroup)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            //检查文件组信息是否合法
            var targetGroup = Entity.FileGroups.Find(id);
            if (targetGroup != null && WebSecurity.IsValidFileGroup(fileGroup))
            {
                targetGroup.Accessible = fileGroup.Accessible;
                targetGroup.Theme = fileGroup.Theme.Trim();
                targetGroup.Description = fileGroup.Description.Trim();
                Entity.SaveChanges();

                return Content(WebHelper.SweetAlert("修改成功", "成功修改文件组!", "location.href='/Admin/ManageFileGroup'"));
            }
            else
            {
                return Content(WebHelper.SweetAlert("修改失败", "提交的信息不合法!"));
            }
        }

        /// <summary>
        /// 删除文件组
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult DelFileGroup(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            var fileGroup = Entity.FileGroups.Find(id);
            if (fileGroup != null)
            {
                Entity.FileGroups.Remove(fileGroup);
                Entity.SaveChanges();
                return Content(WebHelper.SweetAlert("删除成功", "成功删除文件组!"));
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 显示文件组中文件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult ShowGroupFiles(int id, int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            var fileGroup = Entity.FileGroups.Find(id);
            if (fileGroup != null)
            {
                ViewBag.GroupId = id;
                ViewBag.GroupName = fileGroup.Theme;
                var files = fileGroup.WebFiles;
                var pageNumber = page ?? 1;
                ViewBag.PageNumber = pageNumber;
                var onePageOfProducts = files.ToPagedList(pageNumber, 10);
                return View(onePageOfProducts);
            }
            else
            {
                return View("Error");
            }
        }

        #endregion

        #region 文件
        /// <summary>
        /// 已检查文件
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult ManageFile(int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            ViewBag.IsSearch = false;
            var files = Entity.WebFiles.Where(m => m.Checked).OrderByDescending(m => m.Id);
            var pageNumber = page ?? 1;
            ViewBag.PageNumber = pageNumber;
            var onePageOfProducts = files.ToPagedList(pageNumber, 10);
            return View("ManageFile", onePageOfProducts);
        }

        /// <summary>
        /// 待检测文件
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult CheckingFile(int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            var files = Entity.WebFiles.Where(m => !m.Checked).OrderByDescending(m => m.Id);
            var pageNumber = page ?? 1;
            ViewBag.PageNumber = pageNumber;
            var onePageOfProducts = files.ToPagedList(pageNumber, 10);
            return View("CheckingFile", onePageOfProducts);
        }

        /// <summary>
        /// 文件通过检查
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult FilePassCheck(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            var file = Entity.WebFiles.Find(id);
            if (file != null && !file.Checked)
            {
                file.Checked = true;
                file.CheckId = logined.Id;
                Entity.SaveChanges();
                if (Entity.WebFiles.Count(m => !m.Checked) > 0)
                {
                    return RedirectToAction("CheckingFile");
                }
                else
                {
                    return RedirectToAction("ManageFile");
                }
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 阅读文档
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult LookDocument(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            var file = Entity.WebFiles.Find(id);
            if (file != null && file.Type == FileType.Document)
            {
                var path = Server.MapPath("~/WingStudio/Resource/") + file.FilePath;
                if (System.IO.File.Exists(path))
                {

                    file.LookCount++;
                    Entity.SaveChanges();

                    //.doc(x)、.txt、.xls(x)、.ppt(x)、.pdf

                    return File(path, "application/octet-stream", Url.Encode(file.Name));
                }
                else
                {
                    //file.Groups.Clear();
                    Entity.WebFiles.Remove(file);
                    Entity.SaveChanges();
                    return Content(WebHelper.SweetAlert("操作提示", "对不起，该资源已丢失!给你带来困扰，非常抱歉!"));
                }
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult LoadFile(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            var file = Entity.WebFiles.Find(id);
            if (file != null)
            {
                var path = Server.MapPath("~/WingStudio/Resource/") + file.FilePath;
                if (System.IO.File.Exists(path))
                {
                    file.LoadCount++;
                    Entity.SaveChanges();
                    return File(path, "application/octet-stream", Url.Encode(file.Name));
                }
                else
                {
                    //file.Groups.Clear();
                    Entity.WebFiles.Remove(file);
                    Entity.SaveChanges();
                    return Content(WebHelper.SweetAlert("操作提示", "对不起，该资源已丢失!给你带来困扰，非常抱歉!"));
                }
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 播放视频
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult PlayVideo(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            var file = Entity.WebFiles.Find(id);
            if (file != null && file.Type == FileType.Video)
            {
                var path = Server.MapPath("~/WingStudio/Resource/") + file.FilePath;
                if (System.IO.File.Exists(path))
                {
                    file.LookCount++;
                    Entity.SaveChanges();
                    ViewBag.FileName = file.Name;
                    ViewBag.TargetSource = file.FilePath;
                    return View("PlayVideo");
                }
                else
                {
                    //file.Groups.Clear();
                    Entity.WebFiles.Remove(file);
                    Entity.SaveChanges();
                    return Content(WebHelper.SweetAlert("操作提示", "对不起，该资源已丢失!给你带来困扰，非常抱歉!"));
                }
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 搜索博客
        /// </summary>
        /// <param name="searchContent"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult SearchFile(string searchContent, int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageBlog))
            {
                return View("Error");
            }
            ViewBag.IsSearch = true;
            if (string.IsNullOrWhiteSpace(searchContent))
            {
                return View("ManageFile", new List<WebFile>().ToPagedList(1, 10));
            }
            else
            {
                searchContent = searchContent.Trim();
                var files = Entity.WebFiles.Where(m => m.Checked).Where(m => m.Name.Contains(searchContent)).OrderByDescending(m => m.Id);
                var pageNumber = page ?? 1;
                ViewBag.PageNumber = pageNumber;
                var onePageOfProducts = files.ToPagedList(pageNumber, 10);
                ViewBag.SearchContent = searchContent;
                return View("ManageFile", onePageOfProducts);
            }
        }

        #endregion

        /// <summary>
        /// 添加指定文件到指定文件组
        /// </summary>
        /// <param name="id"></param>
        /// <param name="groupId"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult AddFileToGroup(int id, int groupId)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            var file = Entity.WebFiles.Find(id);
            var fileGroup = Entity.FileGroups.Find(groupId);
            if (file != null && fileGroup != null)
            {
                fileGroup.WebFiles.Add(file);
                Entity.SaveChanges();
                return Json(new { title = "添加成功", message = "成功添加指定文件到指定组!" });
            }
            else
            {
                return Json(new { title = "添加失败", message = "提交信息不合法!" });
            }
        }

        /// <summary>
        /// 返回指定组能够添加的文件信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult EnableAddFiles(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            var group = Entity.FileGroups.Find(id);
            if (group != null)
            {
                var dic = new Dictionary<FileType, string> {
                                {FileType.Picture,"图片"},
                                {FileType.Video,"视频"},
                                {FileType.Music,"音乐"},
                                {FileType.Application,"应用"},
                                {FileType.Document,"文档"},
                                {FileType.Other,"其它"},
                            };
                var fileInfos = Entity.WebFiles.Where(m => m.Checked).ToList().Except(group.WebFiles).Select(m => new { m.Id, m.Name, FileType = dic[m.Type], UserAccount = m.Owner.Account}).OrderByDescending(m => m.Id);
                if (fileInfos.Any())
                {
                    return Json((new JavaScriptSerializer()).Serialize(fileInfos));
                }
                else
                {
                    return Json(new { });
                }
            }
            else
            {
                return Json(new { });
            }
        }

        /// <summary>
        /// 返回指定文件能够添加的所有组信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult EnableAddFileGroups(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            var file = Entity.WebFiles.Find(id);
            if (file != null)
            {
                var groupInfos = Entity.FileGroups.ToList().Except(file.Groups).Select(m => new { m.Id, m.Theme, m.Description }).OrderByDescending(m => m.Id);
                if (groupInfos.Any())
                {
                    return Json((new JavaScriptSerializer()).Serialize(groupInfos));
                }
                else
                {
                    return Json(new { });
                }
            }
            else
            {
                return Json(new { });
            }
        }

        /// <summary>
        /// 返回指定文件所属组信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult EnableDelFileGroups(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            var file = Entity.WebFiles.Find(id);
            if (file != null)
            {
                var groupInfos = file.Groups.Select(m => new { m.Id, m.Theme, m.Description }).OrderByDescending(m => m.Id);
                if (groupInfos.Any())
                {
                    return Json((new JavaScriptSerializer()).Serialize(groupInfos));
                }
                else
                {
                    return Json(new { });
                }
            }
            else
            {
                return Json(new { });
            }
        }

        /// <summary>
        /// 从指定文件组中删除指定文件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="groupId"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult DelFileFromGroup(int id, int groupId)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageFile))
            {
                return View("Error");
            }
            var file = Entity.WebFiles.Find(id);
            var fileGroup = Entity.FileGroups.Find(groupId);
            if (file != null && fileGroup != null && fileGroup.WebFiles.Count(m => m.Id == id) > 0)
            {
                fileGroup.WebFiles.Remove(file);
                Entity.SaveChanges();
                return Json(new { title = "删除成功", message = "成功从指定文件组中删除指定文件!" });
                
            }
            else
            {
                return Json(new { title = "添加失败", message = "提交信息不合法!" });
            }
        }


        #endregion

        #region 报名管理
        /// <summary>
        /// 管理报名
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult ManageApplication(Int32? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageApplication))
            {
                return View("Error");
            }
            ViewBag.IsSearch = false;
            var apps = Entity.Applications.OrderByDescending(m => m.Id);
            var pageNumber = page ?? 1;
            ViewBag.PageNumber = pageNumber;
            var onePageOfProducts = apps.ToPagedList(pageNumber, 10);
            return View("ManageApplication", onePageOfProducts);
        }

        /// <summary>
        /// 添加报名
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult AddApplication()
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageApplication))
            {
                return View("Error");
            }
            ViewBag.IsModified = false;
            return View("SaveApplication");
        }

        /// <summary>
        /// 添加报名
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddApplication(Application app)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageApplication))
            {
                return View("Error");
            }
            if ( WebSecurity.IsValidApplication(app))
            {
                var newApp = new Application
                {
                    Publisher = logined,
                    Theme = app.Theme.Trim(),
                    Content = app.Content,
                    StartTime = app.StartTime,
                    EndTime = app.EndTime,
                    IsFormal = app.IsFormal
                };
                Entity.Applications.Add(newApp);
                Entity.SaveChanges();
                InfoLog.Info($"Envent:[添加报名事件] AddApplication[Theme:{newApp.Theme} Id:{newApp.Id}] Operator[Role:Admin Account:{logined.Account} Id:{logined.Id}]");
                return Content(WebHelper.SweetAlert("添加成功", "成功添加报名!", "location.href='/Admin/ManageApplication'"));
            }
            else
            {
                return Content(WebHelper.SweetAlert("添加失败", "提交的信息不合法!"));
            }
        }

        /// <summary>
        /// 修改报名
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult ModApplication(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageApplication))
            {
                return View("Error");
            }
            var app = Entity.Applications.Find(id);
            if (app != null)
            {
                ViewBag.IsModified = true;
                return View("SaveApplication", app);
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 修改报名
        /// </summary>
        /// <param name="id"></param>
        /// <param name="app"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ModApplication(int id, Application app)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageApplication))
            {
                return View("Error");
            }
            var targetApp = Entity.Applications.Find(id);
            if (targetApp != null &&  WebSecurity.IsValidApplication(app))
            {
                targetApp.Publisher = logined;
                targetApp.Theme = app.Theme.Trim();
                targetApp.Content = app.Content;
                targetApp.StartTime = app.StartTime;
                targetApp.EndTime = app.EndTime;
                targetApp.IsFormal = app.IsFormal;
                Entity.SaveChanges();
                InfoLog.Info($"Envent:[修改报名事件] ModApplication[Theme:{targetApp.Theme} Id:{targetApp.Id}] Operator[Role:Admin Account:{logined.Account} Id:{logined.Id}]");
                
                return Content(WebHelper.SweetAlert("修改成功", "成功修改报名!", "location.href='/Admin/ManageApplication'"));
            }
            else
            {
                return Content(WebHelper.SweetAlert("修改失败", "提交的信息不合法!"));
            }
        }

        /// <summary>
        /// 删除报名
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult DelApplication(int id)
        {
           
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageApplication))
            {
                return View("Error");
            }
            var app = Entity.Applications.Find(id);
            if (app != null)
            {
                Entity.Participants.RemoveRange(app.Participants);
                Entity.Applications.Remove(app);
                Entity.SaveChanges();
                InfoLog.Info($"Envent:[删除报名事件] DelApplication[Theme:{app.Theme} Id:{app.Id}] Operator[Role:Admin Account:{logined.Account} Id:{logined.Id}]");
                return Content(WebHelper.SweetAlert("删除成功", "成功删除报名!"));
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 显示报名情况
        /// </summary>
        /// <param name="id"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult ShowParticipants(int id, int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageApplication))
            {
                return View("Error");
            }
            var app = Entity.Applications.Find(id);
            if (app != null)
            {
                ViewBag.IsSearch = false;
                ViewBag.GroupId = id;
                ViewBag.IsFormal = app.IsFormal;
                var pageNumber = page ?? 1;
                ViewBag.PageNumber = pageNumber;
                var onePageOfProducts = app.Participants.OrderByDescending(m => m.Id).ToPagedList(pageNumber, 10);
                return View(onePageOfProducts);
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 从指定报名组中删除指定报名者
        /// </summary>
        /// <param name="id"></param>
        /// <param name="groupId"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult DelParticipant(int id, int groupId, int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageApplication))
            {
                return View("Error");
            }
            var part = Entity.Participants.Find(id);
            var app = Entity.Applications.Find(groupId);
            if (part != null && app != null && part.Application.Id == groupId)
            {
                Entity.Participants.Remove(part);
                Entity.SaveChanges();
                InfoLog.Info($"Envent:[删除报名者事件] DelParticipant[Name:{part.Name} StudentNo:{part.StudentNo}] Operator[Role:Admin Account:{logined.Account} Id:{logined.Id}]");
                return RedirectToAction("ShowParticipants", "Admin", new { id = groupId, page = page ?? 1 });
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 发送邮件给报名者
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult SendEmailToParts(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageApplication))
            {
                return View("Error");
            }
            var app = Entity.Applications.Find(id);
            if(app != null && app.Participants.Any())
            {
                ViewBag.App = app;
                return View();
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 搜索报名人员
        /// </summary>
        /// <param name="id"></param>
        /// <param name="searchContent"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult SearchParticipant(int id, string searchContent, int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageApplication))
            {
                return View("Error");
            }
            var app = Entity.Applications.Find(id);
            if(app == null)
            {
                return View("Error");
            }
            ViewBag.IsSearch = true;
            ViewBag.GroupId = id;
            ViewBag.IsFormal = app.IsFormal;
            if (string.IsNullOrWhiteSpace(searchContent))
            {
                return View("ShowParticipants", new List<Participant>().ToPagedList(1, 10));
            }
            else
            {
                searchContent = searchContent.Trim();
                var parts = app.Participants.Where(m => m.Name.Contains(searchContent) || m.StudentNo.Contains(searchContent)).OrderByDescending(m => m.Id);
                var pageNumber = page ?? 1;
                ViewBag.PageNumber = pageNumber;
                var onePageOfProducts = parts.ToPagedList(pageNumber, 10);
                ViewBag.SearchContent = searchContent;
                return View("ShowParticipants", onePageOfProducts);
            }
        }


        /// <summary>
        /// 搜索报名
        /// </summary>
        /// <param name="searchContent"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult SearchApplication(string searchContent, int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageApplication))
            {
                return View("Error");
            }
            ViewBag.IsSearch = true;
            if (string.IsNullOrWhiteSpace(searchContent))
            {
                return View("ManageApplication", new List<Application>().ToPagedList(1, 10));
            }
            else
            {
                searchContent = searchContent.Trim();
                var apps = Entity.Applications.Where(m => m.Theme.Contains(searchContent)).OrderByDescending(m => m.Id);
                var pageNumber = page ?? 1;
                ViewBag.PageNumber = pageNumber;
                var onePageOfProducts = apps.ToPagedList(pageNumber, 10);
                ViewBag.SearchContent = searchContent;
                return View("ManageApplication", onePageOfProducts);
            }
        }

        #endregion

        #region 消息管理

        /// <summary>
        /// 管理接收的信息
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult ManageReceivedMsg(int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageMessage))
            {
                return View("Error");
            }
            ViewBag.IsSearch = false;
            ViewBag.ReadingMsgCount = Entity.Messages.Count(m => m.Packets.Count(n => !n.Deleted && !n.Read && n.TargetId == 0) > 0);
            var msgs = from msg in Entity.Messages
                       join packet in Entity.Packets
                       on msg.Id equals packet.Message.Id
                       where packet.TargetId == 0 && !packet.Deleted && packet.Read
                       orderby msg.Id descending
                       select msg;
            var pageNumber = page ?? 1;
            ViewBag.PageNumber = pageNumber;
            var onePageOfProducts = msgs.ToPagedList(pageNumber, 10);
            return View("ManageReceivedMsg", onePageOfProducts);
        }

        /// <summary>
        /// 管理发送的信息
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult ManageSentMsg(int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageMessage))
            {
                return View("Error");
            }
            ViewBag.IsSearch = false;
            ViewBag.ReadingMsgCount = Entity.Messages.Count(m => m.Packets.Count(n => !n.Deleted && !n.Read && n.TargetId == 0) > 0);
            var msgs = Entity.Messages.Where(m => m.OwnId == 0 && !m.Deleted).OrderByDescending(m => m.Id);
            var pageNumber = page ?? 1;
            ViewBag.PageNumber = pageNumber;
            var onePageOfProducts = msgs.ToPagedList(pageNumber, 10);
            return View("ManageSentMsg", onePageOfProducts);
        }

        /// <summary>
        /// 收件人信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult ReceiverInfos(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageMessage))
            {
                return View("Error");
            }
            var msg = Entity.Messages.Find(id);
            if(msg != null && !msg.Deleted && msg.OwnId == 0 && msg.Packets.Any())
            {
                var idList = msg.Packets.Select(m => m.TargetId);
                var userInfos = Entity.Users.Where(m => idList.Contains(m.Id)).OrderByDescending(m => m.Id).Select(m => new { m.Account, m.Name});
                if(userInfos.Any())
                {
                    return Json((new JavaScriptSerializer()).Serialize(userInfos));
                }
                else
                {
                    return Json(new { });
                }
            }
            else
            {
                return Json(new { });
            }
        }

        /// <summary>
        /// 管理接收的信息
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult ReadingMsg(int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageMessage))
            {
                return View("Error");
            }
            var msgs = from msg in Entity.Messages
                       join packet in Entity.Packets
                       on msg.Id equals packet.Message.Id
                       where packet.TargetId == 0 && !packet.Deleted && !packet.Read
                       orderby msg.Id descending
                       select msg;
            var msgCount = msgs.Count();
            if (msgCount > 0)
            {
                ViewBag.ReadingMsgCount = msgCount;
            }
            else
            {
                return RedirectToAction("ManageReceivedMsg");
            }
            var pageNumber = page ?? 1;
            ViewBag.PageNumber = pageNumber;
            var onePageOfProducts = msgs.ToPagedList(pageNumber, 10);
            return View("ReadingMsg", onePageOfProducts);
        }

        /// <summary>
        /// 查看信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult LookMessage(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageMessage))
            {
                return View("Error");
            }
            var msg = Entity.Messages.Find(id);
            if (msg != null && msg.OwnId > -1)
            {
                if (msg.OwnId == 0)
                {
                    if (!msg.Deleted)
                    {
                        ViewBag.Owner = "管理员";
                        return View(msg);
                    }
                    else
                    {
                        return View("Error");
                    }
                }
                else
                {
                    var state = msg.Packets.SingleOrDefault(m => m.TargetId == 0);
                    if (state != null && !state.Deleted)
                    {
                        if (!state.Read)
                        {
                            state.Read = true;
                            Entity.SaveChanges();
                        }
                        var ower = Entity.Users.Find(msg.OwnId);
                        if (ower != null) ViewBag.Owner = ower.Account;
                        return View(msg);
                    }
                    else
                    {
                        return View("Error");
                    }
                }
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 标识为已读
        /// </summary>
        /// <param name="id"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult HadRead(int id, int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageMessage))
            {
                return View("Error");
            }
            var msg = Entity.Messages.Find(id);
            if (msg != null && msg.OwnId > 0)
            {
                var state = msg.Packets.SingleOrDefault(m => m.TargetId == 0);
                if (state != null && !state.Deleted)
                {
                    state.Read = true;
                    Entity.SaveChanges();
                    var msgCount = Entity.Messages.Count(m => m.Packets.Count(n => !n.Deleted && !n.Read && n.TargetId == 0) > 0);
                    if(msgCount > 0)
                    {
                        return RedirectToAction("ReadingMsg", new { page = page??1 });
                    }
                    else
                    {
                        return RedirectToAction("ManageReceivedMsg");
                    }
                }
                else
                {
                    return Content(WebHelper.SweetAlert("设置失败", "非法操作!"));
                }
            }
            else
            {
                return Content(WebHelper.SweetAlert("设置失败", "非法操作!"));
            }
        }

        /// <summary>
        /// 发送信息
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult SendMessage()
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageMessage))
            {
                return View("Error");
            }
            ViewBag.IsModified = false;
            return View("SaveMessage");
        }

        /// <summary>
        /// 发送信息
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        [HttpPost]
        
        [ValidateAntiForgeryToken]
        public ActionResult SendMessage(Message msg, string target)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageMessage))
            {
                return View("Error");
            }
            if (!Entity.Users.Any())
            {
                return Content(WebHelper.SweetAlert("发送失败", "无法找到任何目标对象!"));
            }
            if ( WebSecurity.IsValidMessage(msg))
            {
                var newMsg = new Message
                {
                    Content = msg.Content,
                    Theme = msg.Theme.Trim(),
                    OwnId = 0,
                    Packets = new HashSet<Packet>()
                };
                if (string.IsNullOrWhiteSpace(target))
                {
                    foreach (var user in Entity.Users)
                    {
                        var packet = new Packet {TargetId = user.Id};
                        newMsg.Packets.Add(packet);
                    }
                }
                else
                {
                    var subStrs = target.ToLower().Split(new [] { ";" }, StringSplitOptions.RemoveEmptyEntries).Distinct();

                    var userIds = from user in Entity.Users where subStrs.Contains(user.Account) select user.Id;
                    if (userIds.Any())
                    {
                        foreach (var id in userIds)
                        {
                            var packet = new Packet {TargetId = id};
                            newMsg.Packets.Add(packet);
                        }
                    }
                    else
                    {
                        return Content(WebHelper.SweetAlert("发送失败", "无法找到任何目标对象!"));
                    }
                }
                Entity.Messages.Add(newMsg);
                Entity.SaveChanges();
                return Content(WebHelper.SweetAlert("发送成功", "成功发送信息!", "location.href='/Admin/ManageSentMsg'"));
            }
            else
            {
                return Content(WebHelper.SweetAlert("发送失败", "提交的信息不合法!"));
            }
        }

        /// <summary>
        /// 修改信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult ModMessage(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageMessage))
            {
                return View("Error");
            }
            var msg = Entity.Messages.Find(id);
            if (msg != null && msg.OwnId == 0 && !msg.Deleted)
            {
                ViewBag.IsModified = true;
                var target = msg.Packets.Select(item => Entity.Users.Find(item.TargetId)).Where(user => user != null).Aggregate("", (current, user) => current + (user.Account + ";"));
                ViewBag.Target = target;
                return View("SaveMessage", msg);
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 删除信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult DelMessage(int id)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageMessage))
            {
                return View("Error");
            }
            var msg = Entity.Messages.Find(id);
            if (msg != null && msg.OwnId >= 0)
            {
                if (msg.OwnId == 0)
                {
                    if (!msg.Deleted)
                    {
                        msg.Deleted = true;
                    }
                    else
                    {
                        return View("Error");
                    }
                }
                else
                {
                    var state = msg.Packets.SingleOrDefault(m => m.TargetId == 0);
                    if (state != null && !state.Deleted)
                    {
                        state.Deleted = true;
                    }
                    else
                    {
                        return View("Error");
                    }
                }

                Entity.SaveChanges();
                return Content(WebHelper.SweetAlert("删除成功", "成功删除信息!"));
            }
            else
            {
                return View("Error");
            }
        }

        /// <summary>
        /// 搜索信息
        /// </summary>
        /// <param name="searchContent"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult SearchReceivedMsg(string searchContent, int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageMessage))
            {
                return View("Error");
            }
            ViewBag.IsSearch = true;
            ViewBag.ReadingMsgCount = Entity.Messages.Count(m => m.Packets.Count(n => !n.Deleted && !n.Read && n.TargetId == 0) > 0);
            if (string.IsNullOrWhiteSpace(searchContent))
            {
                return View("ManageReceivedMsg", new List<Message>().ToPagedList(1, 10));
            }
            else
            {
                searchContent = searchContent.Trim();
                var msgs = from msg in Entity.Messages
                           join packet in Entity.Packets
                           on msg.Id equals packet.Message.Id
                           where packet.TargetId == 0 && !packet.Deleted && packet.Read && msg.Theme.Contains(searchContent)
                           orderby msg.Id descending
                           select msg;
                var pageNumber = page ?? 1;
                ViewBag.PageNumber = pageNumber;
                var onePageOfProducts = msgs.ToPagedList(pageNumber, 10);
                ViewBag.SearchContent = searchContent;
                return View("ManageReceivedMsg", onePageOfProducts);
            }
        }

        /// <summary>
        /// 搜索信息
        /// </summary>
        /// <param name="searchContent"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult SearchSentMsg(string searchContent, int? page)
        {
            var logined = Loginer;
            if (!logined.HasAuthority(AuthorityFlag.ManageMessage))
            {
                return View("Error");
            }
            ViewBag.IsSearch = true;
            ViewBag.ReadingMsgCount = Entity.Messages.Count(m => m.Packets.Count(n => !n.Deleted && !n.Read && n.TargetId == 0) > 0);
            if (string.IsNullOrWhiteSpace(searchContent))
            {
                return View("ManageSentMsg", new List<Message>().ToPagedList(1, 10));
            }
            else
            {
                searchContent = searchContent.Trim();
                var msgs = Entity.Messages.Where(m => m.OwnId == 0 && !m.Deleted).Where(m => m.Theme.Contains(searchContent)).OrderByDescending(m => m.Id);
                var pageNumber = page ?? 1;
                ViewBag.PageNumber = pageNumber;
                var onePageOfProducts = msgs.ToPagedList(pageNumber, 10);
                ViewBag.SearchContent = searchContent;
                return View("ManageSentMsg", onePageOfProducts);
            }
        }

        #endregion

    }
}
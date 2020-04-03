using RegistrationAndLogin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;

namespace RegistrationAndLogin.Controllers
{
    public class UserController : Controller
    {
        public ActionResult Registraion()
        {
            return View();
        }

        //Registration Post Action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Registraion([Bind(Exclude = "IsEmailVerified,ActivationCode")] User user)
        {
            bool Status = true;
            string message = "";

            //Model Validation

            if (ModelState.IsValid)
            {
                //Email is already exist
                #region 
                var isExist = IsEmailExist(user.EmailID);
                if (isExist)
                {
                    ModelState.AddModelError("EmailExist", "This email already exist!");
                    return View(user);
                }
                #endregion
                //Generate activation code
                #region 
                user.ActivationCode = Guid.NewGuid();
                #endregion
                //Password Hassing
                #region 
                user.Password = Crypto.Hash(user.Password);
                user.ConfirmPassword = Crypto.Hash(user.ConfirmPassword);
                #endregion
                //for the first time 
                user.IsEmailVerified = false;

                //Save Data to Database
                #region
                using (MyDatabaseEntities db = new MyDatabaseEntities())
                {
                    db.Users.Add(user);
                    db.SaveChanges();

                    //Send Email to user

                    SendVerificationLinkEmail(user.EmailID, user.ActivationCode.ToString());
                    message = "Registration successfully done. " +
                              "Account activation link has been sent to your email id:" + user.EmailID;
                    Status = true;
                }
                #endregion

            }
            else
            {
                message = "Invalid request";
            }

            ViewBag.Message = message;
            ViewBag.Status = Status;

            return View(user);
        }

        [NonAction]
        public bool IsEmailExist(string emailID)
        {
            using (MyDatabaseEntities db = new MyDatabaseEntities())
            {
                var v = db.Users.Where(m => m.EmailID == emailID).FirstOrDefault();
                //return v == null ? false : true;
                return v != null;
            }
        }

        [NonAction]
        public void SendVerificationLinkEmail(string emailId, string activationCode)
        {
            var verifyUrl = "/User/VerifyAccount" + activationCode;
            var link = Request.Url.AbsoluteUri.Replace(Request.Url.PathAndQuery, verifyUrl);

            var fromEmail = new MailAddress("mdsoikot484@gmail.com", "Faiz Ahmed");
            var fromEmailPassword = "new1234567890"; //Replace with actual password
            var toEmail = new MailAddress(emailId);
            string subject = "Your account is successfully created";

            string body = "</br></br>We are excited to tell you that your dot net awesome account is" + " successfully created." +
                " Please click on the below link to verify your account" + "</br></br><a href='" + link + "'>" + link + "</a>";


            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromEmail.Address, fromEmailPassword)
            };

            using (var message = new MailMessage(fromEmail, toEmail)
            {
                Subject = subject,
                IsBodyHtml = true,
                Body = body
            })
                smtp.Send(message);
        }
    }
}
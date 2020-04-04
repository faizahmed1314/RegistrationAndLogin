using RegistrationAndLogin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

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
            bool status = false;
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
                    status = true;
                }
                #endregion

            }
            else
            {
                message = "Invalid request";
            }

            ViewBag.Message = message;
            ViewBag.Status = status;

            return View(user);
        }


        //Verify Account
        [HttpGet]
        public ActionResult VerifyAccount(string id)
        {
            bool status = false;
            using(MyDatabaseEntities db=new MyDatabaseEntities())
            {
                //This line i have added here to avoid confirm password does not match issue on save change 
                db.Configuration.ValidateOnSaveEnabled = false;
                var user = db.Users.Where(x => x.ActivationCode == new Guid(id)).FirstOrDefault();
                if (user != null)
                {
                    user.IsEmailVerified = true;
                    db.SaveChanges();
                    status = true;
                }
                else
                {
                    ViewBag.Message = "Invalid Request!";
                }
            }
            ViewBag.Status = status;
            return View();
        }
        //Login
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }
        [HttpPost]
        public ActionResult Login(UserLogin userLogin, string returnUrl)
        {
            string message = "";
            using(MyDatabaseEntities db=new MyDatabaseEntities())
            {
                var user = db.Users.Where(x => x.EmailID == userLogin.EmailID).FirstOrDefault();
                if (user != null)
                {
                    if (string.Compare(Crypto.Hash(userLogin.Password), user.Password) == 0)
                    {
                        int timeOut = userLogin.RememberMe ? 525600 : 20; //525600 min= 1year
                        var ticket = new FormsAuthenticationTicket(userLogin.EmailID, userLogin.RememberMe, timeOut);
                        string encrypted = FormsAuthentication.Encrypt(ticket);
                        var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted);
                        cookie.Expires = DateTime.Now.AddMinutes(timeOut);
                        cookie.HttpOnly = true;
                        Response.Cookies.Add(cookie);

                        if (Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }
                        else
                        {
                            return RedirectToAction("Index", "Home");
                        }
                    }
                    else
                    {
                        message = "Invalid Credential Provided";

                    }
                }
                else
                {
                    message = "Invalid Credential Provided";
                }
            }
            ViewBag.Message = message;
            return View();
        }

        //Logout
        [Authorize]
        [HttpPost]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "User");
        }

        //Forget Password

        public ActionResult ForgetPassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ForgetPassword(string emailID)
        {
            //Verify email id
            
            
            string message = "";
            
            using(MyDatabaseEntities db=new MyDatabaseEntities())
            {
                var account = db.Users.Where(x => x.EmailID == emailID).FirstOrDefault();
                if (account != null)
                {
                    //Send email for reset password
                    string resetCode = Guid.NewGuid().ToString();

                    //Generate reset password link
                    //send email

                    SendVerificationLinkEmail(account.EmailID, resetCode, "ResetPassword");
                    account.ResetPasswordCode = resetCode;

                    //This line I have added here to avoid confirm password not match issue, as we had added a confirm password property 
                  
                    db.Configuration.ValidateOnSaveEnabled = false;
                    db.SaveChanges();
                    message = "Reset password link sent to your email id";
                }
                else
                {
                    message = "Account Not Found!";
                }
            }
            ViewBag.Message = message;
            return View();
        }

        public ActionResult ResetPassword(string id)
        {
            //verify the reset password link
            //Find account associated with this link
            //redirect to reset password link
            using(MyDatabaseEntities db=new MyDatabaseEntities())
            {
                var user = db.Users.Where(x => x.ResetPasswordCode == id).FirstOrDefault();
                if (user != null)
                {
                    ResetPasswordModel model = new ResetPasswordModel();
                    model.ResetCode = id;
                    return View(model);
                }
                else
                {
                    return HttpNotFound();
                }
            }
            
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordModel model)
        {
            var message = "";
            if (ModelState.IsValid)
            {
                using(MyDatabaseEntities db=new MyDatabaseEntities())
                {
                    var user = db.Users.Where(x => x.ResetPasswordCode == model.ResetCode).FirstOrDefault();
                    if (user != null)
                    {
                        user.Password = Crypto.Hash(model.NewPassword);
                        user.ResetPasswordCode = "";
                        db.Configuration.ValidateOnSaveEnabled = false;
                        db.SaveChanges();
                        message = "New password updated successfully.";
                    }
                }
            }
            else
            {
                message = "Somthing Invalid!";
            }
            ViewBag.Message = message;
            return View();
        }

        [NonAction]
        public bool IsEmailExist(string emailID)
        {
            using (MyDatabaseEntities db = new MyDatabaseEntities())
            {
                var user = db.Users.Where(m => m.EmailID == emailID).FirstOrDefault();
                //return v == null ? false : true;
                return user != null;
            }
        }

        [NonAction]
        public void SendVerificationLinkEmail(string emailId, string activationCode, string emailFor= "VerifyAccount")
        {
            var verifyUrl = "/User/"+ emailFor + "/" + activationCode;
            var link = Request.Url.AbsoluteUri.Replace(Request.Url.PathAndQuery, verifyUrl);

            var fromEmail = new MailAddress("mdjubayerahmed042@gmail.com", "Faiz Ahmed");
            var fromEmailPassword = "************"; //Replace with actual password
            var toEmail = new MailAddress(emailId);

            string subject="";
            string body="";

            if(emailFor== "VerifyAccount")
            {
                 subject = "Your account is successfully created";

                 body = "</br></br>We are excited to tell you that your dot net awesome account is" + " successfully created." +
                    " Please click on the below link to verify your account" + "</br></br><a href='" + link + "'>" + link + "</a>";
            }
            else if (emailFor == "ResetPassword")
            {
                subject = "Reset Password";
                body = "Hi,<br/><br/> We got request to reset to your password. Please click on the below link to reset your password"+
                    "<br/><br/><a href='"+link+"'>Reset password link</a>";
            }

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
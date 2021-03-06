﻿using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Webpay.Integration;
using WebpayWS;
using Webpay.Integration.Util.Testing;
using Webpay.Integration.Hosted.Helper;
using Webpay.Integration.Util.Security;
using Webpay.Integration.CONST;
using Webpay.Integration.Order.Row;
using FashionBoutik.DomainServices;
using FashionBoutik.Models;
using FashionBoutik.Entities;
using Microsoft.Extensions.Caching.Memory;
using FashionBoutik.Web.Utils;

namespace FashionBoutik.Web.Controllers
{
    [Area("Admin")]
    //[AutoValidateAntiforgeryToken]
    public class OrderController : Controller
    {
        private readonly IOrderManager _manager;
        private readonly UserManager<User> _userManager;

        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _context; //TODO: can be used instead of MemoryCache

        public OrderController(IOrderManager manager, UserManager<User> userManager, 
            IMemoryCache cache,
            IHttpContextAccessor context)
        {
            _manager = manager;
            _userManager = userManager;

            _cache = cache;
            _context = context;
        }

        [HttpGet]
        [AutoValidateAntiforgeryToken]
        //[Authorize(Roles ="Admin")]
        public async Task<IEnumerable<OrderModel>> List()
        {
            return await _manager.GetAllOrders();//.Include(o => o.Products).Include(o => o.Payment);
        }


        #region private methods

        /// <summary>
        /// TODO: different product may have price in different currencies, so there 
        /// should be a price calculator to convert to currency each product
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        private decimal CalculatePrice(IEnumerable<CartItemModel> lines)
        {
            var ids = lines.Select(l => l.Product.Id);
            return lines.Sum(l => l.Count * l.Product.PricePerUnit.Value);
            //(await _repository.GetProducts() .Where(p => ids.Contains(p.ProductId))
            //.Select(p => lines.First(l => l.ProductId == p.ProductId).Quantity * p.Price)
            //.Sum();
        }

        //private decimal GetTotalPrice(IEnumerable<CartItemModel> lines) {
        //    IEnumerable<long> ids = lines.Select(l => l.ProductId);
        //    return _manager.Products
        //        .Where(p => ids.Contains(p.ProductId))
        //        .Select(p => lines
        //            .First(l => l.ProductId == p.ProductId).Quantity * p.Price)
        //        .Sum();
        //}

        /// <summary>
        /// Identify client and discharge money
        /// </summary>
        /// <param name="payment"></param>
        private void ProcessPayment(PaymentModel payment)
        {
            //TODO: read plastic card data: 
            //1. from ssession
            //2. from the first time: select procrss method
            //  payment.AuthCode = "12345";
        }

        #endregion

        #region Backet \ Shopping Cart Methods

        //[ValidateAntiForgeryToken]
        //[Authorize]
        [HttpGet]
        public async Task<IActionResult> CheckoutDetails(string cartItemsJson)
        {
            var orderedProducts = JsonConvert.DeserializeObject<List<CartItemModel>>(cartItemsJson); //await GetBasketViewModelAsync();

            var userId = _context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);

            var newOrderModel = new OrderModel() {
                OrderItems = orderedProducts,
                BuyerId = currentUser.Id,
                CreatedDate = DateTime.Now,
                Delivered = false,
                //BillingAddress= FILL UP
            };

            //Init & save new order 1st step into cach (for next steps)
            _cache.Set("NewOrder", newOrderModel);
           
            //HttpContext.Session.SetObject("NewOrder", newOrderModel); //HttpContext.Items.Add("NewOrder", newOrderModel);

            return View(new OrderAddressModel()); //go to step 2
        }


        //Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckoutDetails(OrderAddressModel model)
        {
            if (ModelState.IsValid)
            {
                //Update fields of nthe order during wizard
                var newOrderModel = _cache.Get<OrderModel>("NewOrder"); //HttpContext.Session.GetObject<OrderModel>("NewOrder") //((OrderModel)_context.HttpContext.Items["NewOrder"]);
                newOrderModel.Name = model.Name;
                newOrderModel.ShippingAddress = model.ShippingAddress;
                //and save order again (for the next wizard step)
                _cache.Set("NewOrder", newOrderModel);

                //Go the next step 2 - payment options
                return View("СheckoutPayment", new OrderPaymentModel());
            }
            return View(model);// model.OrderItems); //show selected items with errors
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> СheckoutPayment(OrderPaymentModel paymentModel)
        {
            if (ModelState.IsValid)
            {
                //TODO: validate if credit card is valid, balance is enough
                //model.Payment.CardNumber

                //2. Retrieve saved order on the previous steps
                var newOrderModel = _cache.Get<OrderModel>("NewOrder");

                newOrderModel.Payment = new PaymentModel
                {
                    AuthCode = paymentModel.AuthCode,
                    CardNumber = paymentModel.CardNumber,
                    CardExpiry = paymentModel.CardExpiry,
                    CardSecurityCode = paymentModel.CardSecurityCode,
                    Total = paymentModel.Total // TODO: remove - will bre recalculated
                };
                     

                return View("OrderConfirmation", newOrderModel); //go to step of final payment
            }

            return View(paymentModel);
        }

        /// <summary>
        /// Build order -> choose payment type -> DoRequest/GetPaymentForm/PreparePayment
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> OrderConfirmation(OrderModel model)
        {
            if (ModelState.IsValid)
            {
                //Apply discounts, giftcards
                //Call WebPay service
                //var enpoint = PayexService.PxOrderSoapClient.EndpointConfiguration.PxOrderSoap;

                //new PayexService.PxOrderSoapClient(enpoint).CreditOrderLineAsync();// Purchase
                //List<string> excluded = WebpayConnection.CreateOrder(SveaConfig.GetDefaultConfig())
                //                        .SetCountryCode(TestingTool.DefaultTestCountryCode)
                //                        .UsePayPageDirectBankOnly()
                //                        .ConfigureExcludedPaymentMethod()
                //                        .GetExcludedPaymentMethod();
                //string html = BuildDirectBankPayment();
                //var order = TestingTool.CreatePaymentPlanOrderRow();

                // add order rows
                //var firstOrderRowPriceIncVat = 80M;
                /*var firstOrderRowPriceExVat = 64M;
                var firstOrderRowName = "New row #1";
                var firstOrderRowDescription = "This should be the third order row!";

                var firstOrderRow = new OrderRowBuilder()
                    //.SetAmountIncVat(firstOrderRowPriceIncVat)
                    .SetAmountExVat(firstOrderRowPriceExVat)
                    .SetVatPercent(25M)
                    .SetQuantity(1M)
                    .SetDiscountPercent(10)
                    .SetName(firstOrderRowName)
                    .SetDescription(firstOrderRowDescription)
                    ;

                var createOrderBuilder = WebpayConnection.CreateOrder(SveaConfig.GetDefaultConfig())
                    .AddOrderRow(TestingTool.CreatePaymentPlanOrderRow())
                    .AddOrderRow(TestingTool.CreatePaymentPlanOrderRow("2"))
                    .AddCustomerDetails(Item.IndividualCustomer()
                        .SetNationalIdNumber(TestingTool.DefaultTestIndividualNationalIdNumber))
                    .SetCountryCode(TestingTool.DefaultTestCountryCode)
                    .SetOrderDate(TestingTool.DefaultTestDate)
                    .SetClientOrderNumber(TestingTool.DefaultTestClientOrderNumber)
                    .SetCurrency(TestingTool.DefaultTestCurrency)
                    ;*/

                //var order = createOrderBuilder.UsePaymentPlanPayment(campaigns.CampaignCodes[0].CampaignCode).DoRequest();

                MakeCardPayment();

                //await _basketService.SetQuantities(basketViewModel.Id, items);

                //await _orderService.CreateOrderAsync(basketViewModel.Id, basketViewModel.ShippingAddress);

                //await _basketService.DeleteBasketAsync(basketViewModel.Id); //clear shopping bag

                //TODO: if payment transaction succeded
                return View("OrderConfirmationSuccess", model);//show succes page with redirect button to main menu
            }

            return View(model);
        }

        #region Payment methods

        string BuildDirectBankPayment()
        {
            PaymentForm form = WebpayConnection.CreateOrder(SveaConfig.GetDefaultConfig())
                                               .AddOrderRow(TestingTool.CreateExVatBasedOrderRow())
                                               .AddFee(TestingTool.CreateExVatBasedShippingFee())
                                               .AddFee(TestingTool.CreateExVatBasedInvoiceFee())
                                               .AddDiscount(TestingTool.CreateRelativeDiscount())
                                               .AddCustomerDetails(TestingTool.CreateMiniCompanyCustomer())
                                               .SetCountryCode(TestingTool.DefaultTestCountryCode)
                                               .SetOrderDate(TestingTool.DefaultTestDate)
                                               .SetClientOrderNumber(TestingTool.DefaultTestClientOrderNumber)
                                               .SetCurrency(TestingTool.DefaultTestCurrency)
                                               .UsePayPageDirectBankOnly()
                                               .SetReturnUrl("http://myurl.se")
                                               .GetPaymentForm();

            string base64Payment = form.GetXmlMessageBase64();
            string html = Base64Util.DecodeBase64String(base64Payment);

            return html;
            //Assert.That(html.Contains("<amount>18750</amount>"), Is.True);
        }

        void MakeCardPayment()//OrderRowBuilder orderRowBuilder, CountryCode countryCode = CountryCode.SE, string clientOrderNumber = "33")
        {
            PaymentForm form = WebpayConnection.CreateOrder(SveaConfig.GetDefaultConfig())
                                   .AddOrderRow(TestingTool.CreateExVatBasedOrderRow())
                                   .AddFee(TestingTool.CreateExVatBasedShippingFee())
                                   .AddFee(TestingTool.CreateExVatBasedInvoiceFee())
                                   .AddDiscount(TestingTool.CreateRelativeDiscount())
                                   .AddCustomerDetails(TestingTool.CreateMiniCompanyCustomer())
                                   .SetCountryCode(TestingTool.DefaultTestCountryCode)
                                   .SetOrderDate(TestingTool.DefaultTestDate)
                                   .SetClientOrderNumber(TestingTool.DefaultTestClientOrderNumber)
                                   .SetCurrency(TestingTool.DefaultTestCurrency)
                                   .UsePayPageCardOnly()
                                   .SetReturnUrl("http://myurl.se")
                                   .GetPaymentForm();

            //Card payment
            /*PaymentForm form = WebpayConnection.CreateOrder()
                    .AddOrderRow(orderRowBuilder) //required
                    .SetCountryCode(countryCode)  // required
                    .SetClientOrderNumber(clientOrderNumber)
                    .SetOrderDate(DateTime.Now)
                    .UsePaymentMethod(PaymentMethod.SVEACARDPAY)
                    .SetReturnUrl("http://myurl.se") //Required
                    .SetCallbackUrl("http://myurl.se")
                    .SetCancelUrl("http://myurl.se")
                    .GetPaymentForm();*/
        }

        async Task MakeInvoicePayment(OrderRowBuilder orderRowBuilder, WebpayWS.CustomerIdentity customerIdentity, CountryCode countryCode = CountryCode.SE, string clientOrderNumber = "33")
        {
            //Invoice payment
            var response = await WebpayConnection.CreateOrder()
                    .AddOrderRow(orderRowBuilder)//new Webpay.Integration.Order.Row.OrderRowBuilder())                           // required, one or more
                    .AddCustomerDetails(customerIdentity)  // required, individualCustomer or companyCustomer
                    .SetCountryCode(countryCode)   // required
                    .SetOrderDate(DateTime.Now)   // required
                    .UseInvoicePayment()// requires the above attributes in the order
                    .DoRequest();
        }

        #endregion

        /// <summary>
        /// Invoice about current shoppings
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]

        public async Task<IActionResult> СheckoutSummary(CartModel model)
        {
            return PartialView(model);
        }

        /// <summary>
        /// Place a real order after payment
        /// The time of delivery will be in order
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Add(OrderModel order)
        {
            if (ModelState.IsValid)
            {
                order.Delivered = false;
                //order.Payment.Total = await CalculatePrice(order.OrderItems);

                //ProcessPayment(order.Payment);
                if (order.Payment?.AuthCode != null)
                {
                    await _manager.SaveOrder(order);

                    //var userId = httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value
                    var currentUser = await _userManager.GetUserAsync(HttpContext.User);
                    return View("GoToPayment", currentUser);

                    //TODO: then clear shopping bag
                    //await _basketService.DeleteBasketAsync(basketViewModel.Id);
                }
                else
                {
                    //TODO: redirect to registration
                    return Content("Please, verify payment options");
                }
            }

            //AddErrors(ModelState.Error);
            return View(order);
        }


        [HttpPost("{id}")]
        public async Task<bool> MarkShipped(int id)
        {
            var order = await _manager.GetOrderById(id);
            if (order != null)
            {
                order.Delivered = true;
                order.DeliveryDate = DateTime.Now;
                return await _manager.SaveOrder(order);
            }
            return false;
        }

        #endregion
    }
}

//[HttpPost]
//[AllowAnonymous]
//public IActionResult CreateOrder(OrderModel order)
//{
//    if (ModelState.IsValid)
//    {

//        //order.Id = 0;
//        //order.Delivered = false;
//        order.Payment.Total = order.Total //GetTotalPrice

//        //ProcessPayment(order.Payment);
//        if (order.Payment.AuthCode != null)
//        {
//            _manager.Add(order);
//            _manager.SaveChanges();
//            return Ok(new
//            {
//                orderId = order.OrderId,
//                authCode = order.Payment.AuthCode,
//                amount = order.Payment.Total
//            });
//        }
//        else
//        {
//            return BadRequest("Payment rejected");
//        }
//    }
//    return BadRequest(ModelState);
//}
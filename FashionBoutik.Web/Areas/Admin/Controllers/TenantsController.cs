﻿using Microsoft.AspNetCore.Mvc;

namespace FashionBoutik.Web.Areas.Admin.Controllers
{
    //[AbpMvcAuthorize(PermissionNames.Pages_Tenants)]
    [Area("Admin")]
    public class TenantsController : Controller
    {
        private readonly ITenantAppService _tenantAppService;

        public TenantsController(ITenantAppService tenantAppService)
        {
            _tenantAppService = tenantAppService;
        }

        public ActionResult Index()
        {
            var output = _tenantAppService.GetTenants();
            return View(output);
        }
    }
}
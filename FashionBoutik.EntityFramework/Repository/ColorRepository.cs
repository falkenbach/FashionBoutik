﻿using FashionBoutik.Data;
using FashionBoutik.Entities;

namespace FashionBoutik.EntityFramework.Repository
{
    public class ColorRepository : Repository<ColorPallet, int>, IColorRepository
    {
        public ColorRepository(AppDbContext context) : base(context)
        {
        }
    }
}
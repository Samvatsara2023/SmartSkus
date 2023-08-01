﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using SmartSkus.Api.Data.Interface;
using SmartSkus.Api.Models;
using SmartSkus.Shared;
using System.Collections.Generic;
using System.Linq;

namespace SmartSkus.Api.Data.Repo
{
    public class InventoryRepo : IInventoryRepo
    {
        private readonly InventoryContext _context;

        public InventoryRepo(InventoryContext context)
        {
            _context = context;
        }
        public void AddInventory(string sku, string description, long quantity)
        {
            var item = new Item
            {
                ItemCode = sku,
                Name = string.Format("Name: {0}", sku),
                Description = string.Format("Item: {0}", description),
                Region = Region.Africa,
            };

            var findSku = (from s in _context.ItemVariations
                           where s.SKUNumber == sku
                           select s).FirstOrDefault();

            // If SKU exits, increase quantity
            if (findSku != null)
            {

                findSku.Quantity += quantity;
                _context.SaveChanges();
            }
            // Else, Create new
            else
            {
                var variation = new ItemVariation
                {
                    SKUNumber = sku,
                    Description = string.Format("Variation: {0}", description),
                    Price = 0, // Set Deafult price to 0
                    Quantity = quantity,
                    Item = item
                };

                _context.ItemVariations.Add(variation);

                _context.SaveChanges();
            }
        }

        private long CalculateQuantity(long existingQuantity, long newQuantity)
        {
            if (newQuantity < 0)
            {
                if (existingQuantity > (newQuantity * -1))
                {
                    existingQuantity += newQuantity;
                }
                else
                {
                    existingQuantity = 0;
                }
            }
            else
            {
                existingQuantity += newQuantity;
            }

            return existingQuantity;
        }

        public SkuModel UpdateInventory(string sku, long quantity)
        {
            var variationModel = GetVariationBySkuNumber(sku);

            variationModel.Quantity = CalculateQuantity(variationModel.Quantity, quantity);

            _context.SaveChanges();

            var skuModel = GetSkuModelBySkuNumber(sku);
            skuModel.Quantity = CalculateQuantity(skuModel.Quantity, quantity);
            _context.SaveChanges();

            return skuModel;
        }

        public void AddBulkSkus(string sku, List<int> optionKeyIds, int categoryId)
        {
            string Attribute1 = string.Empty;
            string Attribute2 = string.Empty;
            string Attribute3 = string.Empty;

            int count = 1;

            foreach (var i in optionKeyIds)
            {
                string lable = (from x in _context.OptionKeys
                                where x.OptionKeyID == i
                                select x.KeyName).FirstOrDefault();
                if (count == 1)
                {
                    Attribute1 = lable;
                }
                else if (count == 2)
                {
                    Attribute2 = lable;
                }
                else if (count == 3)
                {
                    Attribute3 = lable;
                }

                count++;
            }

            var item = new Item
            {
                ItemCode = sku,
                Name = string.Format("Name: {0}", sku),
                Description = string.Format("Item: {0} - Default description value", sku),
                CategoryID = categoryId,
                Region = Region.Africa,
            };

            char splitChar = '-';

            Dictionary<string, Dictionary<long, string>> keyValuePairs = new();

            foreach (var ok in optionKeyIds)
            {
                var findOptionValue = (from k in _context.OptionValues
                                       where k.OptionKeyID == ok
                                       select new { k.OptionValueID, k.Value }).ToDictionary(t => t.OptionValueID, t => t.Value);

                keyValuePairs.Add(ok.ToString(), findOptionValue);

            }


            Dictionary<string, List<long>> uniqueSkus_len_2 = Utilities.GetAllUniqueSKUs(keyValuePairs, 2, splitChar);

            foreach (var s in uniqueSkus_len_2)
            {
                var variation = new ItemVariation
                {
                    Description = string.Empty,
                    SKUNumber = string.Format("{0}{1}{2}", sku, splitChar, s.Key),
                    Price = 0, // Set Deafult price to 0
                    Quantity = 0, // Set Deafult Quantity to 0 in case of bulk add
                    Item = item
                };

                _context.ItemVariations.Add(variation);

                foreach (long i in s.Value)
                {
                    var optionVariation = new OptionVariations
                    {
                        ItemVariation = variation,
                        OptionValueID = i
                    };

                    _context.OptionVariations.Add(optionVariation);
                }

                var zohoItem = new SkuModel
                {
                    ItemName = sku,
                    Attribute1 = Attribute1,
                    Attribute2 = Attribute2,
                    Attribute3 = Attribute3,
                    GenerateSku = string.Format("{0}{1}{2}", sku, splitChar, s.Key),
                    Item = item
                };
                _context.skuModels.Add(zohoItem);
            }

            _context.SaveChanges();
        }

        public void RemoveQuantity(string skuNumber, long quantity)
        {
            var variationModel = GetVariationBySkuNumber(skuNumber);
            variationModel.Quantity -= quantity;
            _context.SaveChanges();
        }

        public bool SaveChanges()
        {
            return _context.SaveChanges() < 0;
        }

        public ItemVariation GetVariationBySkuNumber(string skuNumber)
        {
            return _context.ItemVariations
                .Include(i => i.Item)
                .FirstOrDefault(s => s.SKUNumber == skuNumber);
        }

        public SkuModel GetSkuModelBySkuNumber(string skuNumber)
        {
            return _context.skuModels
                .Include(i => i.Item)
                .FirstOrDefault(s => s.GenerateSku == skuNumber);
        }

        public IEnumerable<Item> GetAllItems()
        {
            var temp = _context.Items
                .Include(parent => parent.Category)
                .Include(parent => parent.ItemVariations.OrderBy(child => child.ItemVariationID))
                .ToList();

            return temp;
        }

        public IEnumerable<object> GetVariationsOfItems()
        {
            //var sql = $"SELECT i.Name, v.SKUNumber, v.Price, v.Quantity FROM Item as i \r\n  join ItemVariation as v ON\r\n  i.ItemID = v.ItemID";

            var sql = $"SELECT v.* FROM Item as i \r\n  join ItemVariation as v ON\r\n  i.ItemID = v.ItemID";

            //var result = _context.ItemVariations
            //                     .FromSqlRaw(sql)
            //                     .ToList<ItemVariation>();

            var result = (from s in _context.ItemVariations                           
                           select s).ToList();

            return result;
        }

        public bool FindSku(string skuNumber)
        {
            var findSku = (from s in _context.ItemVariations
                           where s.SKUNumber == skuNumber
                           select s).FirstOrDefault();

            if (findSku != null)
            {
                return true;
            }    
            else
            {
                return false;
            }
        }

        public void DeleteAll()
        {
            _context.Database.ExecuteSqlRaw("DELETE FROM dbo.Item");
            _context.Database.ExecuteSqlRaw("DELETE FROM dbo.ItemVariation");
            _context.Database.ExecuteSqlRaw("DELETE FROM dbo.SkuModel");
        }
    }
}

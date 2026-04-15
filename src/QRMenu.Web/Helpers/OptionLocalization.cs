using System.Text.RegularExpressions;

namespace QRMenu.Web.Helpers
{
    public static class OptionLocalization
    {
        private static readonly Dictionary<string, string> TrToEnMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Boyut"] = "Size",
            ["Secenek"] = "Option",
            ["Seçenek"] = "Option",
            ["Ekstralar"] = "Extras",
            ["Soslar"] = "Sauces",
            ["Icecekler"] = "Beverages",
            ["İçecekler"] = "Beverages",
            ["Sut Tipi"] = "Milk Type",
            ["Süt Tipi"] = "Milk Type",
            ["Seker"] = "Sugar",
            ["Şeker"] = "Sugar",

            ["Kucuk"] = "Small",
            ["Küçük"] = "Small",
            ["Orta"] = "Medium",
            ["Buyuk"] = "Large",
            ["Büyük"] = "Large",
            ["Mega"] = "Extra Large",
            ["Sade"] = "Plain",

            ["Buzlu"] = "Iced",
            ["Buzsuz"] = "No Ice",
            ["Az Buzlu"] = "Light Ice",
            ["Sekerli"] = "Sweetened",
            ["Şekerli"] = "Sweetened",
            ["Sekersiz"] = "Unsweetened",
            ["Şekersiz"] = "Unsweetened",
            ["Az Sekerli"] = "Light Sugar",
            ["Az Şekerli"] = "Light Sugar",
            ["Sicak"] = "Hot",
            ["Sıcak"] = "Hot",
            ["Soguk"] = "Cold",
            ["Soğuk"] = "Cold",

            ["Az Pismis"] = "Rare",
            ["Az Pişmiş"] = "Rare",
            ["Orta Pismis"] = "Medium",
            ["Orta Pişmiş"] = "Medium",
            ["Iyi Pismis"] = "Well Done",
            ["İyi Pişmiş"] = "Well Done",

            ["Ketcap"] = "Ketchup",
            ["Ketçap"] = "Ketchup",
            ["Mayonez"] = "Mayonnaise",
            ["Hardal"] = "Mustard",
            ["Ranch Sos"] = "Ranch Sauce",
            ["Barbeku Sos"] = "BBQ Sauce",
            ["Barbekü Sos"] = "BBQ Sauce",
            ["Aci Sos"] = "Hot Sauce",
            ["Acı Sos"] = "Hot Sauce",
            ["Sarımsaklı Mayonez"] = "Garlic Mayo",

            ["Peynir"] = "Cheese",
            ["Cedar Peyniri"] = "Cheddar Cheese",
            ["Çedar Peyniri"] = "Cheddar Cheese",
            ["Cift Peynir"] = "Double Cheese",
            ["Çift Peynir"] = "Double Cheese",
            ["Sogan"] = "Onion",
            ["Soğan"] = "Onion",
            ["Domates"] = "Tomato",
            ["Tursu"] = "Pickles",
            ["Turşu"] = "Pickles",
            ["Marul"] = "Lettuce",

            ["Ketcapsiz"] = "No Ketchup",
            ["Ketçapsız"] = "No Ketchup",
            ["Mayonezsiz"] = "No Mayo",
            ["Sogansiz"] = "No Onion",
            ["Soğansız"] = "No Onion",
            ["Domatessiz"] = "No Tomato",
            ["Tursusuz"] = "No Pickles",
            ["Turşusuz"] = "No Pickles",
            ["Marulsuz"] = "No Lettuce"
        };

        public static string LocalizeOptionText(string? tr, string? en, bool isEn)
        {
            if (!isEn)
            {
                return tr ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(en))
            {
                return en;
            }

            if (string.IsNullOrWhiteSpace(tr))
            {
                return string.Empty;
            }

            var normalized = NormalizeKey(tr);
            if (TrToEnMap.TryGetValue(normalized, out var mapped))
            {
                return mapped;
            }

            return tr;
        }

        private static string NormalizeKey(string value)
        {
            var collapsed = Regex.Replace(value.Trim(), "\\s+", " ");
            return collapsed;
        }
    }
}

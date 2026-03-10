using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRMenu.Data.Migrations
{
    /// <inheritdoc />
    public partial class UrunAciklamaGuncelleme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== SICAK İÇECEKLER =====
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Özenle seçilmiş çekirdeklerden, günlük taze demlenmiş filtre kahve. Yumuşak ve dengeli aroması ile güne güzel bir başlangıç.', ""AciklamaEN"" = 'Freshly brewed daily from hand-picked beans. Smooth and balanced flavor for a great start.' WHERE ""Id"" = 1;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Kadifemsi buharlanmış süt ve tek shot espresso ile hazırlanan klasik latte. Kremamsı dokusu ile favori içeceğiniz.', ""AciklamaEN"" = 'Classic latte with velvety steamed milk and a single shot of espresso. Creamy and smooth.' WHERE ""Id"" = 2;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Yoğun espresso ve sıcak su ile hazırlanan americano. Sade kahve sevenler için güçlü ve aromatik bir tercih.', ""AciklamaEN"" = 'Bold espresso diluted with hot water. Strong and aromatic choice for black coffee lovers.' WHERE ""Id"" = 3;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Demlik çayından demlenen, ince belli bardakta servis edilen geleneksel Türk çayı. Yanında küp şeker ile.', ""AciklamaEN"" = 'Traditional Turkish tea brewed from a double teapot, served in a classic tulip glass with sugar cubes.' WHERE ""Id"" = 42;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Sütlü, tarçın serpilmiş sıcacık sahlep. Soğuk günlerin vazgeçilmez içeceği.', ""AciklamaEN"" = 'Warm salep sprinkled with cinnamon. A cozy classic for cold days.' WHERE ""Id"" = 43;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Gerçek eritme çikolata ile hazırlanan, üzerine çırpılmış krema eklenen sıcak çikolata. Tatlı krizleriniz için birebir.', ""AciklamaEN"" = 'Rich hot chocolate made with real melted chocolate, topped with whipped cream.' WHERE ""Id"" = 44;");

            // ===== SOĞUK İÇECEKLER =====
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Buz gibi soğuk süt üzerine çift shot espresso. Serinletici ve enerji dolu bir içecek.', ""AciklamaEN"" = 'Double shot espresso poured over ice-cold milk. Refreshing and energizing.' WHERE ""Id"" = 5;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Taze sıkılmış limon, şeker ve buz ile hazırlanan ev yapımı limonata. Serinleten doğal lezzet.', ""AciklamaEN"" = 'Homemade lemonade with freshly squeezed lemons, sugar and ice. Naturally refreshing.' WHERE ""Id"" = 6;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Muz, çilek ve yaban mersini karışımı ile hazırlanan taze smoothie. Sağlıklı ve doyurucu.', ""AciklamaEN"" = 'Fresh smoothie blended with banana, strawberry and blueberry. Healthy and filling.' WHERE ""Id"" = 7;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Buzlu süt üzerine tek shot espresso. Hafif ve ferahlatıcı soğuk kahve deneyimi.', ""AciklamaEN"" = 'Single shot espresso over iced milk. Light and refreshing cold coffee experience.' WHERE ""Id"" = 45;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Taze sıkılmış limon ve nane yaprakları ile hazırlanan özel ev yapımı limonata. Serinliğin tarifi.', ""AciklamaEN"" = 'Special homemade lemonade with fresh lemons and mint leaves. The taste of freshness.' WHERE ""Id"" = 46;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Taze mango ve muz ile blender''da hazırlanan tropik smoothie. Yazın en tatlı serinliği.', ""AciklamaEN"" = 'Tropical smoothie blended with fresh mango and banana. The sweetest summer refreshment.' WHERE ""Id"" = 47;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Şeftali veya limon aromalı, buz gibi soğuk buzlu çay. Sıcak günlerin kurtarıcısı.', ""AciklamaEN"" = 'Ice-cold iced tea in peach or lemon flavor. A lifesaver on hot days.' WHERE ""Id"" = 48;");

            // ===== KAHVELER =====
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'İnce öğütülmüş kahve, cezve ile közde pişirilmiş geleneksel Türk kahvesi. Bol köpüklü servis.', ""AciklamaEN"" = 'Finely ground coffee brewed on embers in a traditional cezve. Served with rich foam.' WHERE ""Id"" = 52;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Yoğun ve kısa, tek shot espresso. İtalyan usulü, saf kahve zevki.', ""AciklamaEN"" = 'Intense single shot espresso. Pure Italian-style coffee pleasure.' WHERE ""Id"" = 37;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Espresso üzerine bol köpüklü buharlanmış süt. Kahve ve süt dengesinin mükemmel buluşması.', ""AciklamaEN"" = 'Espresso topped with generous frothed steamed milk. Perfect balance of coffee and milk.' WHERE ""Id"" = 38;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Espresso, çikolata sosu ve buharlanmış süt üçlüsü. Çikolata ve kahve tutkunları için.', ""AciklamaEN"" = 'A trio of espresso, chocolate sauce and steamed milk. For chocolate and coffee lovers.' WHERE ""Id"" = 39;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Çift shot espresso üzerine kadifemsi mikro köpüklü süt. Güçlü kahve aroması, yumuşak doku.', ""AciklamaEN"" = 'Double shot espresso with velvety micro-foamed milk. Bold coffee aroma, silky texture.' WHERE ""Id"" = 40;");

            // ===== KAHVALTI =====
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Beyaz peynir, kaşar, zeytin, bal, kaymak, tereyağı, reçel, yumurta, domates, salatalık ve taze ekmek ile zengin serpme kahvaltı tabağı.', ""AciklamaEN"" = 'Rich breakfast platter with white cheese, aged cheese, olives, honey, cream, butter, jam, eggs, tomatoes, cucumber and fresh bread.' WHERE ""Id"" = 14;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Tereyağında kızartılmış 3 yumurta, yanında taze ekmek ile. Basit ama doyurucu bir lezzet.', ""AciklamaEN"" = 'Three eggs fried in butter, served with fresh bread. Simple yet satisfying.' WHERE ""Id"" = 15;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Taze domates, sivri biber ve yumurta ile hazırlanan geleneksel menemen. Yanında taze ekmek ile servis edilir.', ""AciklamaEN"" = 'Traditional menemen made with fresh tomatoes, green peppers and eggs. Served with fresh bread.' WHERE ""Id"" = 16;");

            // ===== TOSTLAR =====
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Bol kaşar peynirli, çıtır çıtır kızarmış klasik tost. Sade ve lezzetli.', ""AciklamaEN"" = 'Classic crispy toast loaded with melted aged cheese. Simple and delicious.' WHERE ""Id"" = 17;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'İçinde sucuk, kaşar peyniri, domates dilimi ve mısır bulunan dolu dolu karışık tost.', ""AciklamaEN"" = 'Loaded mixed toast with sucuk sausage, aged cheese, tomato slices and corn.' WHERE ""Id"" = 18;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Izgara tavuk göğsü, erimiş kaşar ve taze marul yaprakları ile hazırlanan protein deposu tost.', ""AciklamaEN"" = 'Protein-packed toast with grilled chicken breast, melted cheese and fresh lettuce.' WHERE ""Id"" = 19;");

            // ===== BURGERLER =====
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = '150gr el yapımı dana köfte, taze marul, domates, turşu ve özel burger sosu ile brioche ekmeğinde.', ""AciklamaEN"" = '150g handmade beef patty with fresh lettuce, tomato, pickles and special sauce on a brioche bun.' WHERE ""Id"" = 20;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Dana köfte üzerine erimiş cheddar, karamelize soğan ve özel sos. Peynir tutkunlarının favorisi.', ""AciklamaEN"" = 'Beef patty topped with melted cheddar, caramelized onions and special sauce. A cheese lover''s favorite.' WHERE ""Id"" = 21;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Çıtır panelenmiş tavuk but, taze marul ve ev yapımı mayonez ile. Hafif ve lezzetli alternatif.', ""AciklamaEN"" = 'Crispy breaded chicken thigh with fresh lettuce and homemade mayo. A lighter delicious alternative.' WHERE ""Id"" = 22;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = '2x150gr el yapımı dana köfte, çift kat cheddar peyniri, marul ve domates. Açlığınıza meydan okuyun!', ""AciklamaEN"" = '2x150g handmade beef patties, double cheddar, lettuce and tomato. Challenge your appetite!' WHERE ""Id"" = 23;");

            // ===== PİZZALAR =====
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'İnce hamur üzerine domates sosu, taze mozzarella ve fesleğen yaprakları. İtalya''nın klasiği.', ""AciklamaEN"" = 'Thin crust with tomato sauce, fresh mozzarella and basil leaves. A true Italian classic.' WHERE ""Id"" = 24;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Sucuk, sosis, mantar, yeşil biber ve mısır ile yüklü, herkesin sevdiği karışık pizza.', ""AciklamaEN"" = 'Loaded with sucuk sausage, hot dogs, mushrooms, green pepper and corn. Everyone''s favorite.' WHERE ""Id"" = 25;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Izgara tavuk parçaları, mantar ve mısır ile hazırlanan hafif ve lezzetli pizza.', ""AciklamaEN"" = 'Light and tasty pizza topped with grilled chicken pieces, mushrooms and corn.' WHERE ""Id"" = 26;");

            // ===== MAKARNALAR =====
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Kıymalı domates soslu spagetti. Uzun pişirilen sos ile derinleşen geleneksel İtalyan lezzeti.', ""AciklamaEN"" = 'Spaghetti with slow-cooked beef bolognese sauce. A deep traditional Italian flavor.' WHERE ""Id"" = 27;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Kremamsı alfredo sosu ve ızgara tavuk parçaları ile servis edilen fettuccine. Zengin ve doyurucu.', ""AciklamaEN"" = 'Fettuccine in creamy alfredo sauce with grilled chicken pieces. Rich and satisfying.' WHERE ""Id"" = 28;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Taze fesleğen pesto sosu ile harmanlanan penne makarna. Hafif ve aromatik bir seçim.', ""AciklamaEN"" = 'Penne pasta tossed in fresh basil pesto sauce. A light and aromatic choice.' WHERE ""Id"" = 29;");

            // ===== SALATALAR =====
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Kıvırcık marul, çıtır kruton, parmesan peyniri rendesi ve ev yapımı sezar sosu ile klasik salata.', ""AciklamaEN"" = 'Crisp romaine, crunchy croutons, shaved parmesan and homemade Caesar dressing.' WHERE ""Id"" = 30;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Doğranmış domates, salatalık, soğan ve maydanoz ile taze ve hafif Türk usulü salata.', ""AciklamaEN"" = 'Diced tomatoes, cucumber, onion and parsley. A fresh and light Turkish-style salad.' WHERE ""Id"" = 31;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Ton balık, kıvırcık marul, tatlı mısır ve siyah zeytin ile hazırlanan protein zengini salata.', ""AciklamaEN"" = 'Protein-rich salad with tuna, romaine lettuce, sweet corn and black olives.' WHERE ""Id"" = 32;");

            // ===== TATLILAR =====
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Sıcak sıcak servis edilen, içi akışkan çikolatalı sufle. Yanında vanilya dondurması ile unutulmaz bir deneyim.', ""AciklamaEN"" = 'Warm chocolate fondant with a molten center, served with vanilla ice cream. An unforgettable experience.' WHERE ""Id"" = 33;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Ev yapımı cheesecake üzerine taze frambuaz sosu. Hafif, kremamsı ve ferahlatıcı.', ""AciklamaEN"" = 'Homemade cheesecake drizzled with fresh raspberry sauce. Light, creamy and refreshing.' WHERE ""Id"" = 34;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Tel kadayıf arasında özel peynir ile hazırlanan sıcak künefe, üzerine şerbet ve yanında kaymak dondurma.', ""AciklamaEN"" = 'Hot künefe with special cheese in shredded phyllo, soaked in syrup, served with cream ice cream.' WHERE ""Id"" = 35;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Mascarpone krema, kahveye batırılmış kedi dili bisküvi ve kakao tozu ile İtalyan usulü tiramisu.', ""AciklamaEN"" = 'Italian-style tiramisu with mascarpone cream, coffee-soaked ladyfingers and cocoa powder.' WHERE ""Id"" = 36;");

            // ===== ESKİ ATISTIRMALIKLAR (kat 3) =====
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'New York usulü, kremamsı ve yoğun cheesecake. Üzerine isteğe göre çilek sosu.', ""AciklamaEN"" = 'New York-style, rich and creamy cheesecake. Topped with optional strawberry sauce.' WHERE ""Id"" = 8;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Yoğun çikolatalı, ıslak kıvamda ev yapımı brownie. Yanında dondurma ile mükemmel.', ""AciklamaEN"" = 'Intensely chocolatey, fudgy homemade brownie. Perfect alongside ice cream.' WHERE ""Id"" = 9;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Izgara tavuk, marul, domates, cheddar ve mayonez ile hazırlanan doyurucu kulüp sandviç.', ""AciklamaEN"" = 'Hearty club sandwich with grilled chicken, lettuce, tomato, cheddar and mayo.' WHERE ""Id"" = 10;");

            // ===== ATIŞTIRMALIKLAR (kat 14) =====
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Dışı çıtır içi yumuşak patates kızartması. Özel baharatlar ile tatlandırılmış, ketçap ve mayonez ile servis.', ""AciklamaEN"" = 'Crispy outside, fluffy inside fries seasoned with special spices. Served with ketchup and mayo.' WHERE ""Id"" = 49;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Altın rengi çıtır soğan halkaları, özel pane ile kaplanmış. Ranch sos ile servis edilir.', ""AciklamaEN"" = 'Golden crispy onion rings in special breading. Served with ranch dipping sauce.' WHERE ""Id"" = 50;");
            migrationBuilder.Sql(@"UPDATE ""Urunler"" SET ""Aciklama"" = 'Tavuk göğsünden hazırlanan 8 adet çıtır nugget. BBQ ve ranch sos ile servis edilir.', ""AciklamaEN"" = '8 crispy chicken nuggets made from chicken breast. Served with BBQ and ranch dipping sauces.' WHERE ""Id"" = 51;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eski açıklamalara geri dönüş gerekirse seed data'dan restore edilebilir
        }
    }
}

using Guessnica_backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Guessnica_backend.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IWebHostEnvironment env, UserManager<AppUser> userManager)
    {
        var imagesPath = Path.Combine(env.WebRootPath, "images", "locations");
        Directory.CreateDirectory(imagesPath);

        var locationData = new List<(decimal Lat, decimal Lon, string Desc, string Url, string File)>
        {
            (51.2070m, 16.1550m, "Rynek w Legnicy", "https://images.unsplash.com/photo-1590503831101-55cdf1464dc6", "rynek.jpg"),
            (51.2063m, 16.1586m, "Zamek Piastowski", "https://images.unsplash.com/photo-1585952295628-95ac5e5eef42", "zamek.jpg"),
            (51.2088m, 16.1502m, "Park Miejski", "https://images.unsplash.com/photo-1519331379826-f10be5486c6f", "park.jpg"),
            (51.2095m, 16.1611m, "Katedra Świętych Apostołów Piotra i Pawła", "https://images.unsplash.com/photo-1548625149-fc4a29cf7092", "katedra.jpg"),
            (51.2072m, 16.1565m, "Ratusz miejski", "https://images.unsplash.com/photo-1555992336-fb0d29498b13", "ratusz.jpg"),
            (51.2042m, 16.1598m, "Brama Głogowska", "https://images.unsplash.com/photo-1577207404389-43a40797e63e", "brama_glogowska.jpg"),
            (51.2105m, 16.1490m, "Teatr Modrzejewskiej", "https://images.unsplash.com/photo-1503095396549-807759245b35", "teatr.jpg"),
            (51.2085m, 16.1625m, "Pomnik Bitwy Legnickiej", "https://images.unsplash.com/photo-1567522173839-e91806015fb3", "pomnik_bitwy.jpg"),
            (51.2078m, 16.1538m, "Kamienica Śledziowa", "https://images.unsplash.com/photo-1558618666-fcd25c85cd64", "kamienica_sledziowa.jpg"),
            (51.2068m, 16.1572m, "Plac Słowiański", "https://images.unsplash.com/photo-1541888946425-d81bb19240f5", "plac_slowianski.jpg"),
            (51.2058m, 16.1448m, "Dworzec PKP Legnica", "https://images.unsplash.com/photo-1474487548417-781cb71495f3", "dworzec.jpg"),
            (51.2112m, 16.1555m, "Park Kopernika", "https://images.unsplash.com/photo-1510798831971-661eb04b3739", "park_kopernika.jpg"),
            (51.2045m, 16.1520m, "Fontanna na Placu Wilsona", "https://images.unsplash.com/photo-1547471080-7cc2caa01a7e", "fontanna_wilson.jpg"),
            (51.2092m, 16.1642m, "Kościół Mariański", "https://images.unsplash.com/photo-1520250497591-112f2f40a3f4", "kosciol_marianski.jpg"),
            (51.2118m, 16.1578m, "Cmentarz Komunalny", "https://images.unsplash.com/photo-1533113414723-e1df0beb0744", "cmentarz.jpg"),
            (51.2082m, 16.1595m, "Wieża Ciśnień", "https://images.unsplash.com/photo-1513635269975-59663e0ac1ad", "wieza_cisnien.jpg"),
            (51.2055m, 16.1612m, "Kościół św. Jana", "https://images.unsplash.com/photo-1605104185614-074e27e4aca3", "kosciol_jana.jpg"),
            (51.2098m, 16.1528m, "Galeria Piastów", "https://images.unsplash.com/photo-1519567241046-7f570eee3ce6", "galeria_piastow.jpg"),
            (51.2075m, 16.1605m, "Ulica Najświętszej Marii Panny", "https://images.unsplash.com/photo-1477959858617-67f85cf4f1df", "ulica_nmp.jpg"),
            (51.2035m, 16.1555m, "Amfiteatr miejski", "https://images.unsplash.com/photo-1524368535928-5b5e00ddc76b", "amfiteatr.jpg")
        };

        try
        {
            // SEEDOWANIE LOKALIZACJI
            Console.WriteLine("Starting to seed locations...");

            if (await db.Locations.CountAsync() >= locationData.Count)
            {
                Console.WriteLine("Locations already exist, skipping location seed.");
            }
            else
            {
                await SeedLocationsAsync(locationData, db, env);
            }

            // SEEDOWANIE ZAGADEK
            Console.WriteLine("Starting to seed riddles...");

            var savedLocations = await db.Locations.OrderBy(l => l.Id).ToListAsync();
            if (await db.Riddles.CountAsync() < savedLocations.Count)
            {
                await SeedRiddlesAsync(savedLocations, db);
            }
            else
            {
                Console.WriteLine("Riddles already exist, skipping riddle seed.");
            }

            // SEEDOWANIE UŻYTKOWNIKÓW
            await SeedUsersAsync(db, userManager);
            Console.WriteLine("Database seeding completed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during seed: {ex.Message}");
            throw; // Ew. logowanie większej ilości szczegółowości
        }
    }

    private static async Task SeedLocationsAsync(
        List<(decimal Lat, decimal Lon, string Desc, string Url, string File)> locationData,
        AppDbContext db, 
        IWebHostEnvironment env)
    {
        var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var locations = new List<Location>();
        var imagesPath = Path.Combine(env.WebRootPath, "images", "locations");

        foreach (var (lat, lon, desc, imageUrl, fileName) in locationData)
        {
            var filePath = Path.Combine(imagesPath, fileName);
            var webImageUrl = $"/images/locations/{fileName}";

            if (!File.Exists(filePath))
            {
                try
                {
                    Console.WriteLine($"Downloading image: {fileName}");
                    var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                    await File.WriteAllBytesAsync(filePath, imageBytes);
                    Console.WriteLine($"Successfully downloaded: {fileName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to download image {fileName}: {ex.Message}");
                    webImageUrl = "/images/locations/placeholder.jpg";
                }
            }

            var locationExists = await db.Locations.AnyAsync(l => l.Latitude == lat && l.Longitude == lon && l.ShortDescription == desc);
            if (!locationExists)
            {
                locations.Add(new Location
                {
                    Latitude = lat,
                    Longitude = lon,
                    ShortDescription = desc,
                    ImageUrl = webImageUrl
                });
            }
        }

        if (locations.Any())
        {
            await db.Locations.AddRangeAsync(locations);
            await db.SaveChangesAsync();
            Console.WriteLine($"Added {locations.Count} locations to database.");
        }
    }

    private static async Task SeedRiddlesAsync(List<Location> savedLocations, AppDbContext db)
    {
        var riddleDescriptions = new List<(string Desc, RiddleDifficulty Diff, int Time, int Dist)>
        {
                ("Jesteś w samym sercu Legnicy. Spójrz na kolorowe kamienice.", RiddleDifficulty.Easy, 120, 300),
                ("Historyczna siedziba Piastów. Gdzie jesteś?", RiddleDifficulty.Medium, 90, 200),
                ("Dużo zieleni, alejki i cisza. To miejsce zna każdy legniczanin.", RiddleDifficulty.Hard, 60, 150),
                ("Najważniejsza świątynia w mieście. Jej wieże widać z daleka.", RiddleDifficulty.Medium, 100, 200),
                ("Budynek z zegarem, miejsce władzy miejskiej od wieków.", RiddleDifficulty.Easy, 110, 250),
                ("Zabytkowa brama obronna, jedna z nielicznych zachowanych.", RiddleDifficulty.Hard, 80, 150),
                ("Tu wystawia się spektakle. Budynek nosi imię wielkiej aktorki.", RiddleDifficulty.Medium, 95, 180),
                ("Upamiętnia bitwę z 1241 roku. Stoi przy ruchliwej ulicy.", RiddleDifficulty.Hard, 75, 140),
                ("Zabytkowa kamienica z ciekawą nazwą związaną z rybami.", RiddleDifficulty.Hard, 70, 120),
                ("Reprezentacyjny plac w centrum, miejsce wydarzeń kulturalnych.", RiddleDifficulty.Easy, 105, 220),
                ("Tutaj rozpoczyna się i kończy podróż pociągiem.", RiddleDifficulty.Easy, 115, 280),
                ("Park ze stuletnimi drzewami, nosi imię astronoma.", RiddleDifficulty.Medium, 85, 190),
                ("Woda tryska wysoko w centrum placu nazwanego imieniem prezydenta USA.", RiddleDifficulty.Medium, 90, 175),
                ("Gotycka świątynia, Maryja jest jej patronką.", RiddleDifficulty.Hard, 80, 160),
                ("Miejsce wiecznego spoczynku, pełne pomników i historii.", RiddleDifficulty.Medium, 100, 250),
                ("Wysoka budowla przemysłowa, dawniej zaopatrywała miasto w wodę.", RiddleDifficulty.Hard, 85, 180),
                ("Średniowieczny kościół z czerwonej cegły, patron apostoł.", RiddleDifficulty.Medium, 95, 190),
                ("Centrum handlowe nazwane od średniowiecznej dynastii.", RiddleDifficulty.Easy, 125, 300),
                ("Główna ulica handlowa, prowadzi do rynku.", RiddleDifficulty.Medium, 100, 200),
                ("Obiekt na świeżym powietrzu, tu odbywają się koncerty latem.", RiddleDifficulty.Hard, 90, 170)
            };

        var riddles = new List<Riddle>();
        for (int i = 0; i < riddleDescriptions.Count && i < savedLocations.Count; i++)
        {
            var (desc, diff, time, dist) = riddleDescriptions[i];
            var location = savedLocations[i];

            var riddleExists = await db.Riddles.AnyAsync(r => r.Description == desc && r.LocationId == location.Id);
            if (!riddleExists)
            {
                riddles.Add(new Riddle
                {
                    Description = desc,
                    Difficulty = diff,
                    TimeLimitSeconds = time,
                    MaxDistanceMeters = dist,
                    LocationId = location.Id
                });
            }
        }

        if (riddles.Any())
        {
            await db.Riddles.AddRangeAsync(riddles);
            await db.SaveChangesAsync();
            Console.WriteLine($"Added {riddles.Count} riddles to database.");
        }
    }

    private static async Task SeedUsersAsync(AppDbContext db, UserManager<AppUser> userManager)
    {
        var testUsers = new List<(string Email, string DisplayName, string Password)>
        {
                ("anna.kowalska@example.com", "Anna Kowalska", "User123!"),
                ("jan.nowak@example.com", "Jan Nowak", "User123!"),
                ("maria.wisniewski@example.com", "Maria Wiśniewski", "User123!"),
                ("piotr.kaminski@example.com", "Piotr Kamiński", "User123!"),
                ("katarzyna.lewandowska@example.com", "Katarzyna Lewandowska", "User123!"),
                ("tomasz.zielinski@example.com", "Tomasz Zieliński", "User123!"),
                ("magdalena.szymanska@example.com", "Magdalena Szymańska", "User123!"),
                ("krzysztof.wozniak@example.com", "Krzysztof Woźniak", "User123!"),
                ("joanna.kowalczyk@example.com", "Joanna Kowalczyk", "User123!"),
                ("marcin.kozlowski@example.com", "Marcin Kozłowski", "User123!"),
                ("aleksandra.jankowska@example.com", "Aleksandra Jankowska", "User123!"),
                ("adam.wojciechowski@example.com", "Adam Wojciechowski", "User123!"),
                ("ewa.kwiatkowska@example.com", "Ewa Kwiatkowska", "User123!"),
                ("lukasz.kaczmarek@example.com", "Łukasz Kaczmarek", "User123!"),
                ("agnieszka.mazur@example.com", "Agnieszka Mazur", "User123!"),
                ("pawel.krawczyk@example.com", "Paweł Krawczyk", "User123!"),
                ("beata.piotrowski@example.com", "Beata Piotrowski", "User123!"),
                ("daniel.grabowski@example.com", "Daniel Grabowski", "User123!"),
                ("marta.nowakowska@example.com", "Marta Nowakowska", "User123!"),
                ("robert.michalski@example.com", "Robert Michalski", "User123!")
        };

        var existingEmails = await db.Users
            .Where(u => testUsers.Select(tu => tu.Email).Contains(u.Email))
            .Select(u => u.Email)
            .ToListAsync();

        var newTestUsers = testUsers
            .Where(t => !existingEmails.Contains(t.Email))
            .ToList();

        int createdCount = 0;
        foreach (var (email, displayName, password) in newTestUsers)
        {
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                DisplayName = displayName,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 365))
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "User");
                createdCount++;
            }
            else
            {
                Console.WriteLine($"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        Console.WriteLine($"Created {createdCount} new test users.");
    }
}
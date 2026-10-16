using DmcGrid.Core.Interfaces;
using DmcGrid.Core.Models;
using DmcGrid.Core.Services;

namespace DmcGrid.Data;

/// <summary>Seeds the demo accounts and the Thailand / Malaysia / Bali RFQs from the approved prototype. Runs only on an empty Users table.</summary>
public class DemoSeeder(Db db, IRfqRepository rfqs, IQuoteRepository quotes)
{
    public async Task SeedAsync()
    {
        if (await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Users") > 0) return;

        var agent = await AddUserAsync("agent@dmcgrid.demo", "Agent@123", "Demo Agent", Roles.Agent, "GoLuxigo Luxury Holidays",
            "27ABCDE1234F1Z5", "+91 98765 43210", "", leads: 15, wallet: 1100, credits: 0);
        var dmc1 = await AddUserAsync("dmc1@dmcgrid.demo", "Dmc@123", "DMC One", Roles.Dmc, "Siam Horizons DMC",
            "", "", "Thailand,Malaysia,Bali,Singapore", 0, 0, 21);
        var dmc2 = await AddUserAsync("dmc2@dmcgrid.demo", "Dmc@123", "DMC Two", Roles.Dmc, "Andaman Routes DMC",
            "", "", "Thailand,Bali,Dubai", 0, 0, 20);
        var dmc3 = await AddUserAsync("dmc3@dmcgrid.demo", "Dmc@123", "DMC Three", Roles.Dmc, "Lotus Premium Journeys",
            "", "", "", 0, 0, 20);

        // RFQ-1048 · Thailand — 5 options from 3 DMCs, awaiting the agent's review.
        var thailand = await rfqs.SaveAsync(new Rfq
        {
            AgentUserId = agent, Destination = "Thailand", TravelYear = 2026, TravelMonth = "October",
            StartDate = new DateTime(2026, 10, 12), EndDate = new DateTime(2026, 10, 18), Adults = 2,
            HotelCategory = "4★", RoomType = "Deluxe", MealPlan = "Breakfast", Cities = "Bangkok / Pattaya / Phuket",
            Vehicle = "Private", FoodPrefs = "Vegetarian,Indian", Budget = "₹1–2L",
            Priorities = "Central hotels · private transfers · relaxed itinerary",
            NaturalText = "Thailand in October for 2 adults, 6 nights. Central 4-star hotels, private car, vegetarian Indian food. Keep it relaxed, budget around ₹2 lakh.",
            AiFields = "destination,year,month,adults,duration,hotel,vehicle,food,budget,priorities",
            Status = RfqStatus.Published
        });
        await quotes.SaveAsync(thailand, dmc1,
        [
            ThailandPackage(0, "Value 3★", 118000, "3★ Deluxe", "Deluxe", "Private Car", "Vegetarian"),
            ThailandPackage(1, "Comfort 4★", 136000, "4★ Deluxe", "Deluxe", "Private Car", "Veg + Indian")
        ], submit: true);
        await quotes.SaveAsync(thailand, dmc2,
            [ThailandPackage(2, "Comfort Plus 4★", 142500, "4★ Deluxe", "Deluxe", "Camry / similar", "Veg + Indian")], submit: true);
        await quotes.SaveAsync(thailand, dmc3,
        [
            ThailandPackage(3, "Premium 4★+", 169000, "4★ Premium", "Premium", "Premium SUV", "Veg + Indian"),
            ThailandPackage(4, "Luxury 5★", 192000, "5★ Premium", "Suite", "Luxury SUV", "Customized Indian")
        ], submit: true);

        // RFQ-1049 · Malaysia — quotes received.
        var malaysia = await rfqs.SaveAsync(new Rfq
        {
            AgentUserId = agent, Destination = "Malaysia", TravelYear = 2026, TravelMonth = "November",
            StartDate = new DateTime(2026, 11, 3), EndDate = new DateTime(2026, 11, 10), Adults = 2, Children = 1,
            HotelCategory = "4★", RoomType = "Family", MealPlan = "Breakfast", Cities = "Kuala Lumpur / Langkawi",
            Vehicle = "Private", FoodPrefs = "Halal", Budget = "₹2–3L", Priorities = "Family friendly · theme parks",
            Status = RfqStatus.Published
        });
        await quotes.SaveAsync(malaysia, dmc1,
        [
            SimplePackage("Classic 4★", 168000, "4★ Deluxe", "Private Car", "Halal",
                ("Kuala Lumpur", "Airport arrival & private transfer · Petronas Towers evening"),
                ("Kuala Lumpur", "Batu Caves · Genting Highlands day trip"),
                ("Langkawi", "Flight to Langkawi · beach resort check-in")),
            SimplePackage("Family Premium 4★", 214000, "4★ Premium", "Private Van", "Halal + Indian",
                ("Kuala Lumpur", "Private van transfer · KL Tower dinner"),
                ("Kuala Lumpur", "Sunway Lagoon theme park · fast-track tickets"),
                ("Langkawi", "Flight to Langkawi · SkyCab & island hopping"))
        ], submit: true);
        await quotes.SaveAsync(malaysia, dmc3,
        [
            SimplePackage("Signature 5★", 252000, "5★ Premium", "Luxury SUV", "Halal",
                ("Kuala Lumpur", "Luxury transfer · private city highlights"),
                ("Kuala Lumpur", "Private Genting excursion · chef's table dinner"),
                ("Langkawi", "Flight to Langkawi · sunset cruise"))
        ], submit: true);

        // RFQ-1050 · Bali — already connected to two DMCs.
        var bali = await rfqs.SaveAsync(new Rfq
        {
            AgentUserId = agent, Destination = "Bali", TravelYear = 2026, TravelMonth = "November",
            StartDate = new DateTime(2026, 11, 21), EndDate = new DateTime(2026, 11, 26), Adults = 2,
            HotelCategory = "Private Villa", RoomType = "Villa", MealPlan = "Breakfast", Cities = "Ubud / Seminyak",
            Vehicle = "Private", FoodPrefs = "Vegetarian", Budget = "₹2–3L", Priorities = "Honeymoon · private pool villa",
            Status = RfqStatus.Published
        });
        await quotes.SaveAsync(bali, dmc1,
        [
            SimplePackage("Ubud & Seminyak Villas", 186000, "Private Pool Villa", "Private Car", "Vegetarian",
                ("Ubud", "Airport pickup · private villa check-in · floating breakfast"),
                ("Ubud", "Tegalalang rice terraces · Ubud monkey forest · spa"),
                ("Seminyak", "Transfer to Seminyak · beach club sunset"))
        ], submit: true);
        await quotes.SaveAsync(bali, dmc2,
        [
            SimplePackage("Honeymoon Signature", 228000, "5★ Pool Villa", "Private Car", "Vegetarian + Indian",
                ("Ubud", "VIP airport transfer · jungle villa check-in · candlelight dinner"),
                ("Ubud", "Private sunrise trek · couples spa · swing experience"),
                ("Seminyak", "Transfer to Seminyak · private beach dinner"))
        ], submit: true);
        var baliPackages = await quotes.GetSubmittedAsync(bali);
        await quotes.ConnectAsync(bali, agent, baliPackages.Select(p => p.PackageId).ToList(), "lead");
    }

    private Task<int> AddUserAsync(string email, string password, string name, string role, string org, string gst, string phone,
        string destinations, int leads, decimal wallet, int credits) =>
        db.ScalarAsync<int>("""
            INSERT INTO dbo.Users (Email, PasswordHash, FullName, Role, OrgName, GstNumber, Phone, Destinations, LeadsRemaining, WalletBalance, QuoteCredits)
            VALUES (@email, @hash, @name, @role, @org, @gst, @phone, @dest, @leads, @wallet, @credits);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            Db.P("@email", email), Db.P("@hash", PasswordHasher.Hash(password)), Db.P("@name", name), Db.P("@role", role),
            Db.P("@org", org), Db.P("@gst", gst), Db.P("@phone", phone), Db.P("@dest", destinations), Db.P("@leads", leads),
            Db.P("@wallet", wallet), Db.P("@credits", credits));

    private static readonly string[] ThailandCities = ["Bangkok", "Bangkok", "Pattaya", "Pattaya", "Phuket", "Phuket", "Phuket"];

    // Day-wise summaries per package option (columns: Value 3★, Comfort 4★, Comfort Plus, Premium, Luxury).
    private static readonly string[][] ThailandSummaries =
    [
        ["Airport pickup · Private sedan · 3★ Sukhumvit hotel · Free evening", "Airport pickup · Private sedan · 4★ central hotel · Chao Phraya dinner cruise", "Camry pickup · 4★ Riverside hotel · Asiatique evening", "Luxury transfer · 4★ Premium Riverside · Rooftop dinner", "BMW transfer · 5★ Riverside suite · Private welcome dinner"],
        ["Grand Palace · Wat Pho · shared city tour · evening free", "Grand Palace · Wat Arun · private city tour · IconSiam evening", "Private temples tour · local lunch · IconSiam", "Private guide · temples · Michelin lunch · rooftop sunset", "Private guide & luxury van · temples · chef-led Thai dinner"],
        ["Road transfer · 3★ Pattaya hotel · free evening", "Private transfer · 4★ beach hotel · Alcazar Show", "Camry transfer · 4★ sea-view hotel · Sanctuary of Truth", "Premium SUV · 4★+ beachfront · Sanctuary + dinner", "Luxury SUV · 5★ resort · private sunset yacht"],
        ["Shared speedboat · Coral Island · Indian lunch", "Speedboat Coral Island · lunch · private hotel transfers", "Premium speedboat · island · lunch · Nong Nooch", "Private speedboat · water-sports credit · beach lunch", "Private yacht · island hopping · onboard lunch"],
        ["Bangkok connection · Phuket · 3★ Patong hotel", "Domestic flight · private transfer · 4★ Patong", "Flight to Phuket · Camry · 4★ Kata · leisure", "Flight · Premium SUV · 5★ resort · sunset dinner", "Business-class sector · luxury transfer · 5★ pool villa"],
        ["Shared big boat Phi Phi · lunch · standard transfers", "Premium speedboat Phi Phi · lunch · shared transfers", "Speedboat Phi Phi + Maya Bay · private hotel transfer", "Early-bird premium Phi Phi · Maya Bay · premium lunch", "Private speedboat · flexible timing · gourmet picnic · photographer"],
        ["Shared airport transfer · departure assistance", "Private sedan to airport · departure", "Private Camry · airport drop · assistance", "Premium SUV · airport drop · fast-track", "Luxury transfer · airport concierge · fast-track departure"]
    ];

    // Timed plans for days 1–2, per package option.
    private static readonly (string From, string To, string What)[][][] ThailandTimed =
    [
        [
            [("10:30", "12:00", "Airport pickup & transfer"), ("14:00", "15:00", "3★ hotel check-in"), ("15:00", "17:00", "Sukhumvit orientation walk")],
            [("10:30", "12:00", "Private airport transfer"), ("14:00", "15:00", "4★ central hotel check-in"), ("15:00", "17:00", "IconSiam visit"), ("19:00", "21:00", "Chao Phraya dinner cruise")],
            [("10:15", "11:45", "Camry airport transfer"), ("14:00", "15:00", "Riverside hotel check-in"), ("15:00", "17:00", "Asiatique visit"), ("18:30", "20:30", "Riverfront dinner")],
            [("10:00", "11:30", "Luxury airport transfer"), ("14:00", "15:00", "Premium hotel check-in"), ("15:00", "17:00", "Spa / leisure"), ("19:30", "21:00", "Rooftop dinner")],
            [("09:45", "11:15", "BMW airport transfer"), ("14:00", "15:00", "5★ suite check-in"), ("15:00", "17:00", "Private concierge orientation"), ("19:00", "21:00", "Private welcome dinner")]
        ],
        [
            [("09:00", "11:00", "Grand Palace"), ("11:00", "12:30", "Wat Pho"), ("14:00", "16:00", "City tour")],
            [("10:00", "12:00", "Grand Palace"), ("12:00", "13:00", "Wat Arun"), ("14:00", "16:00", "IconSiam"), ("19:00", "21:00", "Dinner cruise")],
            [("09:30", "11:30", "Grand Palace"), ("11:30", "13:00", "Wat Arun"), ("13:30", "14:30", "Local lunch"), ("15:00", "17:00", "IconSiam")],
            [("10:30", "12:30", "Private temple tour"), ("13:00", "14:30", "Michelin lunch"), ("15:00", "17:00", "Premium shopping"), ("18:00", "20:00", "Rooftop sunset")],
            [("10:00", "12:30", "Private guided temples"), ("13:00", "14:30", "Chef lunch"), ("15:00", "17:00", "Private city experience"), ("19:00", "21:00", "Chef-led dinner")]
        ]
    ];

    private static readonly (string From, string To)[] DefaultSlots =
        [("09:30", "12:30"), ("13:00", "14:30"), ("15:00", "17:00"), ("18:30", "20:30")];

    private static QuotePackage ThailandPackage(int option, string name, decimal price, string hotel, string room, string vehicle, string food)
    {
        var package = new QuotePackage { Name = name, NetPrice = price, Hotel = hotel, Room = room, Meal = "Breakfast", Vehicle = vehicle, Food = food };
        for (var day = 0; day < ThailandSummaries.Length; day++)
        {
            var timed = day < ThailandTimed.Length
                ? ThailandTimed[day][option]
                : ThailandSummaries[day][option].Split(" · ").Select((what, i) => (DefaultSlots[i % DefaultSlots.Length].From, DefaultSlots[i % DefaultSlots.Length].To, what)).ToArray();
            package.Days.Add(timed.Select(x => new PackageActivity
            {
                From = x.Item1, To = x.Item2, City = ThailandCities[day], Desc = x.Item3, Transfer = vehicle
            }).ToList());
        }
        return package;
    }

    private static QuotePackage SimplePackage(string name, decimal price, string hotel, string vehicle, string food,
        params (string City, string Summary)[] days)
    {
        var package = new QuotePackage { Name = name, NetPrice = price, Hotel = hotel, Room = "Deluxe", Meal = "Breakfast", Vehicle = vehicle, Food = food };
        foreach (var (city, summary) in days)
        {
            package.Days.Add(summary.Split(" · ").Select((what, i) => new PackageActivity
            {
                From = DefaultSlots[i % DefaultSlots.Length].From, To = DefaultSlots[i % DefaultSlots.Length].To,
                City = city, Desc = what, Transfer = vehicle
            }).ToList());
        }
        return package;
    }
}

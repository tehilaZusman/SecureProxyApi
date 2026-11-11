# Secure Proxy Web API – ממשק API מאובטח לתיווך בקשות

ממשק Proxy מאובטח, מבוסס תצורה, עם ברירות מחדל מחמירות ומדיניות **Fail Closed**, שנבנה באמצעות **.NET 8 / ASP.NET Core / EF Core / SQL Server**.

ה־Proxy מעביר רק פעולות שהוגדרו במפורש אל יעדים מוגדרים מראש. הלקוח בוחר פעולה באמצעות מפתח (`clientId`), אך אינו יכול לבחור בעצמו את כתובת היעד, את מתודת ה־HTTP או את הנתיב. שום בקשה אינה מועברת לשירות חיצוני לפני שכל בדיקות האימות והמדיניות הסתיימו בהצלחה.

## דרישות מקדימות

- .NET 8 SDK (הפתרון נבנה גם באמצעות .NET 10 SDK; הפרויקטים עצמם מכוונים ל־`net8.0`).
- SQL Server להפעלת ה־API (LocalDB, Express או שרת מלא). **אין צורך ב־SQL Server כדי להריץ את הבדיקות.**
- כלי `dotnet-ef` הגלובלי, רק אם רוצים לייצר מחדש מיגרציות:
  `dotnet tool install --global dotnet-ef --version 8.0.10`

## מבנה הפתרון – שלוש שכבות (API / BL / DAL)

```text
SecureProxyApi.sln
├─ src/SecureProxy.Dal        שכבת הגישה לנתונים
│  ├─ Domain                  ישויות EF
│  ├─ Data                    DbContext + נתוני אתחול
│  ├─ Repositories            מאגרים + Unit of Work
│  ├─ DependencyInjection.cs  רישום שירותי DAL
│  └─ Migrations              מיגרציות EF Core
├─ src/SecureProxy.Bl         שכבת הלוגיקה העסקית (תלויה ב־DAL)
│  ├─ Dtos                    ProxyExecuteRequest – קלט ללוגיקה
│  ├─ Security                IKeySource + הצפנת AES-GCM
│  └─ Proxy                   אימות, בניית URL, מדיניות כותרות, מנהל ביצוע, Audit ו־Cache
├─ src/SecureProxy.Api        שכבת ה־API (תלויה ב־BL וב־DAL)
│  ├─ Dtos                    ProxyErrorResponse – פלט HTTP
│  ├─ Controllers             ProxyController, OperationsAdminController
│  └─ Program.cs              הרכבת האפליקציה ורישום תלויות
└─ tests/SecureProxy.Tests    בדיקות xUnit: מדיניות, הצפנה, שירות חיצוני, Audit, Concurrency ו־E2E
```

כיוון התלויות הוא חד־כיווני: **API ← BL ← DAL** במובן של שימוש בשכבות התחתונות: שכבת ה־API משתמשת ב־BL, וה־BL משתמשת ב־DAL. ה־DAL אינה מכירה את ה־BL או את ה־API. ה־`DbContext` ומיגרציות EF נמצאים ב־DAL.

## הגדרת מסד הנתונים

מחרוזת החיבור נמצאת בקובץ `src/SecureProxy.Api/appsettings.json` תחת `ConnectionStrings:ProxyDb`. ברירת המחדל היא LocalDB:

```text
Server=(localdb)\MSSQLLocalDB;Database=SecureProxy;Trusted_Connection=True;TrustServerCertificate=True
```

יש לשנות אותה בהתאם למופע SQL Server שברשותכם.

### החלת מבנה מסד הנתונים

אפשרות א' – מיגרציות EF (מומלץ):

```bash
dotnet ef database update \
  --project src/SecureProxy.Dal/SecureProxy.Dal.csproj \
  --startup-project src/SecureProxy.Api/SecureProxy.Api.csproj
```

ה־`--project` מצביע על DAL משום ששם נמצאים ה־`DbContext` והמיגרציות. ה־`--startup-project` מצביע על API משום ששם מוגדרים מחרוזת החיבור ורישום התלויות.

בסביבת `Development`, ה־API מחיל גם הוא את המיגרציות ומוסיף נתוני דוגמה בעת עליית האפליקציה.

אפשרות ב' – SQL ישיר: להריץ את הקובץ `db/schema.sql` מול מסד הנתונים.

## הפעלת ה־API

```bash
dotnet run --project src/SecureProxy.Api/SecureProxy.Api.csproj
```

ממשק Swagger זמין בנתיב `/swagger` בסביבת Development. כברירת מחדל, ה־API מאזין בכתובת:

```text
http://localhost:5080
```

### סודות ואימות API

מפתחות API וחומר מפתח ההצפנה אינם נשמרים בקובץ `appsettings.json` ואינם נכללים במאגר הקוד. יש להגדיר סודות מקומיים באמצעות .NET User Secrets. יש להריץ את הפקודות משורש המאגר:

```bash
dotnet user-secrets init --project src/SecureProxy.Api/SecureProxy.Api.csproj
dotnet user-secrets set "Proxy:ApiKey" "<a-long-random-consumer-key>" --project src/SecureProxy.Api/SecureProxy.Api.csproj
dotnet user-secrets set "Admin:ApiKey" "<a-different-long-random-admin-key>" --project src/SecureProxy.Api/SecureProxy.Api.csproj
dotnet user-secrets set "Encryption:Keys:Customer" "<base64-encoded-32-byte-key>" --project src/SecureProxy.Api/SecureProxy.Api.csproj
```

פעולת הדוגמה `post.create` משתמשת בהפניה למפתח הצפנה `config:Customer`. רכיב `IKeySource` מאתר את המפתח דרך ספק הסודות שהוגדר. יעד יכול גם להגדיר `AuthProfileRef` כגון `config:CustomerApi` או `env:UPSTREAM_CUSTOMER_BEARER`; ספק פרופילי האימות מאתר את הסוד ומוסיף אותו כ־Bearer token. אם פרופיל האימות חסר או לא תקין, הבקשה נדחית לפני העברה לשירות החיצוני.

בסביבת Production יש לספק את ההגדרות דרך מנהל סודות מאושר או משתני סביבה מוגנים, לדוגמה: `Proxy__ApiKey`, `Admin__ApiKey`, `Encryption__Keys__Customer` ו־`UpstreamAuth__Profiles__CustomerApi`. **אין להכניס סודות אמיתיים לקוד או למאגר.**

כל קריאה ל־`/api/proxy/execute` דורשת את הכותרת `X-Proxy-Api-Key`. פעולות ניהול דורשות בנפרד את `X-Admin-Api-Key`. במימוש ההדגמה נעשה שימוש במפתחות משותפים כדי לשמור על דוגמה פשוטה. בסביבת Production עדיף להשתמש ב־OIDC/OAuth2 עם זהות נפרדת לכל צרכן, scopes/roles, מנגנוני החלפה וביטול של מפתחות.

## שליחת בקשה דרך ה־Proxy

פעולת הדוגמה `customer.get` מעבירה בקשת `GET /users/{urlId}` אל:
`https://jsonplaceholder.typicode.com`

```bash
curl -X POST http://localhost:5080/api/proxy/execute \
  -H "Content-Type: application/json" \
  -H "X-Proxy-Api-Key: <consumer-api-key>" \
  -d '{ "clientId": "customer.get", "parameters": { "urlId": 1 } }'
```

פעולת הדוגמה `post.create` דורשת Payload מוצפן באמצעות AES-GCM. פורמט הנתונים המועברים מפורט בקובץ `DESIGN-NOTES.md`.

### ניהול: הפעלה והשבתה של פעולה עם בקרת מקביליות אופטימית

נקודות הקצה הניהוליות דורשות מפתח Admin בכותרת `X-Admin-Api-Key`. המפתח מוגדר באמצעות User Secrets או מנהל סודות תחת `Admin:ApiKey`. מפתח חסר מחזיר **401**, מפתח שגוי מחזיר **403**, ומפתח שרת שלא הוגדר גורם לסגירה בטוחה של נקודת הקצה עם **503**. בכל מקרי הדחייה, הגדרות הפעולה נשארות ללא שינוי.

```bash
curl -X PATCH http://localhost:5080/api/admin/operations/customer.get/enabled \
  -H "Content-Type: application/json" \
  -H "X-Admin-Api-Key: <admin-api-key>" \
  -d '{ "isActive": false, "rowVersion": "<base64 rowversion you last read>" }'
```

ערך `rowVersion` מיושן מחזיר **409 Conflict**.

> מנגנון מפתחות ה־API נועד לפשט את משימת ההערכה. בפריסת Production יש להשתמש באימות אמיתי (OIDC/JWT) ובהרשאות מבוססות תפקידים, ולתעד פעולות ניהול.

## הרצת הבדיקות

```bash
dotnet test
```

הבדיקות משתמשות ב־**SQLite in-memory** (ולא ב־SQL Server) וב־HTTP handler מתעד. בכל בקשה שנדחית, הבדיקות מוודאות שה־handler המדמה את השירות החיצוני לא קיבל אף בקשה. קוד סטטוס לבדו אינו נחשב הוכחה לכך שהבקשה נחסמה.

## מסמכי הפרויקט

- `DESIGN-NOTES.md` – הארכיטקטורה וההחלטות הטכניות המרכזיות.
- `DEFECT-REPORT.md` – תקלות שנבחנו והאופן שבו התכנון מונע אותן.
- `AI-USAGE.md` – הצהרה על השימוש בכלי בינה מלאכותית.

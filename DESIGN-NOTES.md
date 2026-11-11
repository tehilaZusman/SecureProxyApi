# מסמך תכנון – Secure Proxy API

## ארכיטקטורה: שלוש שכבות

הפתרון מחולק לשלושה פרויקטים, עם שרשרת תלויות חד־כיוונית:

**API → BL → DAL**

- **DAL (`SecureProxy.Dal`) – שכבת הגישה לנתונים:** אחראית לשמירה ולשליפה בלבד. כוללת ישויות EF בתיקיית `Domain`, את `ProxyDbContext`, את מנגנון הוספת נתוני הדוגמה, מיגרציות EF Core, מאגרים ו־Unit of Work בתיקיית `Repositories`. אין בה כללי לוגיקה עסקית, HTTP או הצפנה. היא אינה תלויה ב־BL או ב־API.
- **BL (`SecureProxy.Bl`) – שכבת הלוגיקה העסקית:** מכילה את כל הלוגיקה העסקית: אימות מעטפת ופרמטרים, הצפנת Payload באמצעות AES-GCM והפשטת מקור המפתחות, מדיניות Allowlist לכותרות, בניית כתובות URL מהימנות, cache תצורה, Audit ואת `ProxyExecutor` שמתזמר את רצף העיבוד. היא תלויה ב־DAL בלבד. אובייקט הקלט ללוגיקה `ProxyExecuteRequest` נמצא בשכבה זו.
- **API (`SecureProxy.Api`) – שכבת HTTP:** שכבת HTTP דקה הכוללת Controllers, את `Program.cs` (הרכבת תלויות והגבלות בקשות), תצורה ו־Swagger. אובייקט השגיאה של HTTP, בשם `ProxyErrorResponse`, נמצא בשכבה זו. היא תלויה ב־BL וב־DAL; התלות הישירה ב־DAL משמשת לרישום `DbContext` ולהפעלת מיגרציות בעת עליית האפליקציה בלבד.

מדוע `ProxyExecuteRequest` נמצא ב־BL ואילו `ProxyErrorResponse` נמצא ב־API? הבקשה היא קלט ישיר לפעולה העסקית `IProxyExecutor.ExecuteAsync`, ולכן ה־BL צריכה להחזיק בה בלי להיות תלויה ב־API. לעומת זאת, תגובת השגיאה היא ייצוג HTTP בלבד, ולכן היא נשארת ב־API ואינה מחלחלת לשכבת ה־BL.

ה־`DbContext` והמיגרציות נמצאים בכוונה ב־DAL. לכן כלי EF משתמשים ב־`--project src/SecureProxy.Dal` יחד עם `--startup-project src/SecureProxy.Api` (פרויקט ה־Startup מספק את מחרוזת החיבור ואת רישום התלויות).

## גישה לנתונים: Repository ו־Unit of Work

כל הגישה למסד הנתונים מתבצעת דרך Repositories. השכבות העליונות אינן ניגשות ישירות ל־`DbContext`. כך EF Core נשארת מוסתרת בתוך DAL, וקל יותר להחליף או לדמות את פעולות הקריאה והכתיבה בבדיקות.

- **Repositories** חושפים מתודות ששמן מבטא את הכוונה, ולא שאילתות גולמיות:
  - `IOperationRepository.GetByKeyAsync` – חיפוש לקריאה בלבד (`AsNoTracking`), שטוען מראש גם את היעד, כללי הפרמטרים, כללי הכותרות ופרופיל ההצפנה. משמש בתהליך הטיפול בבקשה.
  - `IOperationRepository.GetTrackedByKeyAsync` – חיפוש עם מעקב שינויים, המיועד לתהליכי כתיבה ועדכון.
  - `IOperationRepository.SetExpectedRowVersion` – מגדיר את נקודת הייחוס לבקרת מקביליות אופטימית (ערך ה־rowversion שהלקוח קרא בעבר) בתור `OriginalValue` של EF. כך עדכון המבוסס על ערך ישן נכשל בעת השמירה. מנגנון מעקב השינויים נשאר בתוך DAL, במקום לחשוף `DbContext.Entry(...)` אל ה־API.
  - `IRequestLogRepository.Add` ו־`IConfigurationChangeLogRepository.Add` – מוסיפים לרישום הממתין לשמירה רשומות Audit והיסטוריית שינויים בתצורה.
- **Unit of Work** (`IUnitOfWork.SaveChangesAsync`) הוא נקודת ה־Commit היחידה. ה־Repositories רק מכינים שינויים (`Add` או שינוי של ישויות במעקב); כל השינויים שהוכנו במהלך הבקשה נשמרים בקריאה אחת ל־`SaveChanges`, ולכן משתתפים באותה טרנזקציה. `UnitOfWork` עוטף כשלים ב־`DataAccessException`, אך מעביר הלאה ללא שינוי `DbUpdateConcurrencyException`, כדי שה־API יוכל להמיר התנגשות מקביליות ל־HTTP 409.
- **בידוד שגיאות:** `OperationRepository` עוטף כשלים בשאילתות בתוך `DataAccessException`. כך ה־BL וה־API תלויות בסוג חריגה שבבעלות DAL ולא בחריגות ספציפיות של EF או של ספק מסד הנתונים.
- **רישום שירותים:** `DependencyInjection.AddDalServices(configuration)` רושם במקום אחד את `DbContext` (SQL Server), את ה־Repositories ואת Unit of Work. ב־`Program.cs` נדרשת רק קריאה למתודה זו.

איך cache התצורה משתלב? `CachedConfigurationProvider` שב־BL תלוי ב־`IOperationRepository` ולא ב־`DbContext`. ה־Repository מבצע את השליפה הגולמית, וה־Provider מוסיף מעליה cache בזיכרון ומנגנון ביטול cache. כך נשמרת הפרדה בין "שליפה ממסד הנתונים" (DAL) לבין "מדיניות הcache" (BL).

## עקרונות מנחים

1. **מבוסס תצורה:** היעד, מתודת HTTP והנתיב נקבעים בתצורה מהימנה. הלקוח מספק רק מפתח פעולה ופרמטרים בעלי שמות מוגדרים.
2. **אבטחה כברירת מחדל / Fail Closed:** כל כשל באימות או במדיניות עוצר את התהליך לפני העברת הבקשה. בדיקות ה־Recording Handler מוכיחות שבמקרה של דחייה לא נשלחת בקשה לשירות החיצוני.
3. **חסימה כברירת מחדל (Deny by Default):** פרמטרים לא מוכרים וכותרות שאינן ברשימת ההיתרים נדחים, אלא אם הפעולה הוגדרה במפורש אחרת.

## רצף עיבוד הבקשה

`ProxyExecutor.RunAsync` מבצע את השלבים הבאים לפי הסדר. כל כשל עוצר את התהליך לפני העברת הבקשה לשירות החיצוני:

1. **אימות מעטפת (Envelope):** בדיקה שהשדה `clientId` קיים.
2. **איתור פעולה:** חיפוש לפי מפתח באמצעות `IProxyConfigurationProvider` (כולל Cache).
3. **בדיקת הפעלה:** בדיקה שהפעולה פעילה ושהיעד קיים ופעיל.
4. **מיזוג `urlId`:** שדה הנוחות `urlId` ממוזג לתוך `parameters`, אך עדיין עובר את כל בדיקות האימות.
5. **הצפנה:** אם הפעולה דורשת הצפנה, ה־Payload חייב להיות קיים ולעבור פענוח עם תג AES-GCM תקין. כשל מחזיר `invalid_encrypted_payload`; הבקשה אינה מועברת.
6. **אימות פרמטרים:** בדיקת פרמטרים חובה ואופציונליים, המרות טיפוסים, אורך וטווח, Regex, מדיניות פרמטרים לא מוכרים וחסימת Path Traversal בפרמטרים של נתיב.
7. **בניית URL מהימן:** שילוב כתובת בסיס ותבנית נתיב, תוך הצבת פרמטרים של נתיב שעברו אימות בלבד. ה־URL הסופי נבדק שוב כדי לוודא שהוא תואם ל־Host, ל־Scheme ול־Base Path שהוגדרו, וכן לפורט מותר.
8. **סינון headers:** העברת headers לפי Allowlist.
9. **העברה לשירות החיצוני:** שימוש ב־Timeout ייעודי ליעד, אגירת תגובה מוגבלת וביטול לפי הצורך.

## מדוע הלקוח אינו יכול לשנות את כתובת היעד

כתובת היעד, מתודת HTTP והנתיב מגיעים אך ורק מרשומות `Destination` ו־`Operation`. קלט הלקוח היחיד שמשמש לבניית ה־URL הוא **פרמטרי נתיב/Query שעברו אימות**.

`UpstreamUrlBuilder` מבצע בדיקת הגנה נוספת: לאחר בניית ה־URI הוא מוודא שה־Host וה־Scheme עדיין תואמים ליעד שהוגדר, ושהנתיב נשאר תחת Base Path שהוגדר. כל ניסיון לחרוג ממנו נדחה עם `path_traversal`. ערכי Path ו־Query עוברים Percent Encoding. הפניות אוטומטיות (Redirects) מושבתות ב־HttpClient, כך שהשירות החיצוני אינו יכול להפנות את ה־Proxy ל־Host אחר.

## הצפנה – AES-GCM

פורמט הנתונים המועברים, לאחר קידוד Base64:

```text
[12-byte nonce][ciphertext][16-byte tag]
```

- לכל הודעה נוצר Nonce אקראי באורך 96 ביט ותג אימות באורך 128 ביט.
- בעת פענוח מתבצע אימות של ה־Tag. שינוי בנתונים, מפתח שגוי או קלט פגום גורמים ל־`PayloadDecryptionException` ולדחיית הבקשה. כשלים קריפטוגרפיים ממופים להודעת שגיאה כללית ללקוח, כדי שלא לחשוף פרטים על המפתח או על האלגוריתם.
- **המפתחות עצמם לעולם אינם נשמרים במסד הנתונים.** `EncryptionProfile.KeyRef` הוא הפניה בלבד (`env:` או `config:`), שאותה פותר `IKeySource`. מסד הנתונים מכיל מטא־נתונים בלבד. בסביבת Production יש לחבר את `IKeySource` למנהל סודות.

## מדיניות headers

`HeaderPolicy.Filter` מעביר header רק אם הפעולה כוללת אותה ברשימת ההיתרים **וגם** היא אינה נמצאת בקבוצת ה headers החסומות תמיד: `Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie` וכותרות Hop-by-hop (`Connection`, `Keep-Alive`, `Transfer-Encoding`, `TE`, `Trailer`, `Upgrade`, `Host`, `Content-Length`, `Proxy-Connection`). קבוצת החסימה תמיד גוברת, גם אם הוגדרה Allowlist שגויה.

## אמינות ועמידות

### Timeouts וביטול בקשות

כל העברה משתמשת ב־`CancellationTokenSource` עם Timeout שהוגדר ליעד, המקושר ל־Token של הלקוח. המערכת מבחינה בין שני מצבים:

- אם **הלקוח** ביטל את הבקשה – התוצאה היא `499 caller_cancelled`.
- אם ה־**Timeout** של ה־Proxy פג – התוצאה היא `504 upstream_timeout`.

הערך `HttpClient.Timeout` מוגדר כ־Infinite, כך שמקור האמת היחיד למדידת Timeout הוא ה־CTS שלנו. ה־Token של הלקוח הוא `RequestAborted` של ASP.NET, מועבר אל `ProxyController.Execute` ומשם אל `ProxyExecutor.ExecuteAsync`. בדיקת E2E בשם `Caller_cancellation_propagates_from_controller_to_executor` מבטלת בקשה אמיתית שנמצאת בתהליך ומוודאת שרשומת ה־Audit שנשמרה מציגה את תוצאת 499 `caller_cancelled`. כך נבדק החיבור בין הרכיבים, ולא רק הלוגיקה הפנימית של ה־Executor.

### מחזור חיים של HttpClient

נעשה שימוש ב־`IHttpClientFactory` עם לקוח בשם `upstream` ו־`SocketsHttpHandler` שמוגדר בו `PooledConnectionLifetime`. לא נוצרת מופע חדש של `HttpClient` לכל בקשה.

### Streaming ומגבלות גודל

גודל גוף הבקשה מוגבל בשלוש נקודות:

1. **Kestrel** – מגבלה ברמת התעבורה (`MaxRequestBodySize`), הנגזרת מ־`Proxy:MaxRequestBytes`.
2. **מגבלה לפי פעולה** – `MaxRequestBytes` של הפעולה נבדק מול **הגודל האמיתי של גוף בקשת ה־HTTP המקורית**: נעשה שימוש ב־`Content-Length` תקין כאשר הוא קיים, אחרת נספרים הבתים באמצעות Stream שאינו מבצע Buffering. הבדיקה כוללת גם שדות JSON שה־Model Binding התעלם מהם, ומתבצעת **לפני** פענוח או אימות פרמטרים.
3. **גוף JSON שנבנה מחדש** – מתבצעת בדיקה נוספת של הגודל לפני שליחתו לשירות החיצוני.

אכיפת המגבלה על ה־Payload הנכנס תחילה מבטיחה שבקשה גדולה מדי תידחה עם `413 request_too_large` ו־`ForwardingOccurred = false`, עוד לפני השקעת משאבים בפענוח או באימות. תגובות השירות החיצוני נקראות באמצעות `ResponseHeadersRead` ומועתקות במקטעים של 80 KiB, עד `MaxResponseBytes`. חריגה מהמגבלה עוצרת את התהליך עם `response_too_large`, כדי למנוע אגירה בלתי מוגבלת בזיכרון.

### מקביליות

עדכוני תצורה משתמשים ב־Optimistic Concurrency:

- ב־SQL Server נעשה שימוש ב־`rowversion` מובנה.
- ב־SQLite ובמסד הנתונים שבזיכרון (בבדיקות) נעשה שימוש ב־Token מקביליות שמנוהל באפליקציה, עם אותה משמעות.

עדכון על בסיס גרסה ישנה גורם ל־`DbUpdateConcurrencyException`, שמוחזר כ־**409 Conflict** עם הקוד `configuration_conflict`.

בקשות שכבר טענו הגדרה של פעולה או יעד לפני שהושבתו יכולות להסתיים על בסיס ה־Snapshot שלהן. בקשות חדשות יראו את השינוי לאחר ביטול המטמון או פקיעת תוקפו.

### Resilience – עמידות בפני עומס ותקלות

**ממומש בקוד:** הגבלת מספר החיבורים המקבילים לשירות חיצוני. ה־`SocketsHttpHandler` של לקוח `upstream` מגדיר `MaxConnectionsPerServer` לפי `Proxy:MaxConnectionsPerServer` (ברירת מחדל 64, ערך שמרני). כך מוגבל מספר החיבורים המקבילים שה־Proxy פותח לכל Host חיצוני, ושירות חיצוני איטי או תקול אינו יכול לכלות את כל תקציב החיבורים/Socket של ה־Proxy.

זוהי עמידות אמיתית שאינה משכפלת קריאות שאינן Idempotent: היא מגבילה מקביליות ואינה מבצעת Retry.

## טיפול בשגיאות

שגיאות מוחזרות במבנה אחיד:

`ProxyErrorResponse { error, code, correlationId }`

השדה `code` מכיל ערכים יציבים שאינם רגישים (`ReasonCodes`). פרטי Host פנימיים, פרטי התחברות, Stack Traces ופרטי הצפנה אינם נחשפים ללקוח.

### Content-Type

ה־`[Consumes("application/json")]` של הקונטרולר הוא שער האימות הראשי: גוף שאינו JSON נדחה בגבול ה־HTTP עם `415 Unsupported Media Type`, לפני הפעלת ה־Executor (ללא שינוי בהתנהגות הקיימת).

כהגנה נוספת, `ProxyExecutor` מאמת גם את ערך `contentType` שהועבר אליו. Content-Type שאינו JSON ואינו ריק נדחה עם `415 invalid_content_type` ואינו מועבר הלאה.

סוג המדיה נבדק ללא הפרמטרים הנלווים, ולכן `application/json; charset=utf-8` מתקבל. ערך Content-Type ריק או null (למשל בקריאת Unit Test ישירה שאין בה מטא־נתונים של גוף HTTP) נחשב תקין, כדי שגבול ה־HTTP יישאר נקודת האכיפה היחידה עבור בקשות אמיתיות.

קודי הסטטוס של השירות החיצוני **נשמרים** כאשר בוצעה העברה. כשל שנוצר בתוך ה־Proxy (Timeout, שגיאת חיבור או חריגה ממגבלת גודל) משתמש בקוד 5xx/499 נפרד וב־`ForwardingOccurred = false`. כך תמיד אפשר להבחין בין תגובה של השירות החיצוני לבין שגיאה פנימית של ה־Proxy.

## Audit, Logging וניטור

כל ניסיון מתועד באופן מובנה עם Correlation ID: המערכת מכבדת ערך שהגיע   בheader `X-Correlation-Id`, או מייצרת מזהה חדש אם לא נשלח כזה.

המידע נשמר ב־`RequestLogs` וכולל: Correlation ID, מפתח פעולה, יעד, זמני התחלה וסיום, משך הביצוע, האם הבקשה הועברה, סטטוס השירות החיצוני, סטטוס ה־Proxy, קטגוריית תוצאה וקוד סיבה שאינו רגיש.

למודל `RequestLog` **אין שדה שיכול להכיל כותרות, גופי בקשות, מפתחות או פרטי התחברות**. כך מבנה המודל עצמו מונע שמירת מידע רגיש בלוג Audit.

### מדיניות Audit כאשר SQL Server אינו זמין

שמירת Audit היא במאמץ מיטבי (Best effort). `DatabaseAuditSink` תופס כשלים בשמירה, רושם אותם באמצעות Logger מובנה וממשיך בלי להשליך את הבקשה. כך תקלה בשכבת האחסון אינה משנה את תוצאת HTTP של בקשה שכבר התקבלה לגביה החלטת מדיניות.

בזמן השבתה של מסד הנתונים, ייתכנו פערים ביומן Audit הקבוע, אך ה־Proxy נשאר זמין ולוג האפליקציה עדיין מתעד כל ניסיון. אם סביבת עבודה מחייבת שמירת Audit בכל מצב, אפשר להחליף את ה־Sink במימוש שנכשל יחד עם הבקשה כאשר אין אפשרות לשמור (Fail Closed עבור Audit). שינוי כזה דורש החלפה של מימוש ממשק אחד.

## היסטוריית שינויים בתצורה

`ConfigurationChangeLogs` מתעדת את סוג הישות, מזהה הישות, הפעולה שבוצעה, תקציר שאינו רגיש (למשל `IsActive: true -> false`), מי ביצע את השינוי ומתי.

במנגנון מפתח Admin משותף, כפי שממומש עבור משימת ההערכה, `ChangedBy` מכיל את זהות השירות `admin-api-key` ולא זהות של אדם מסוים. כדי לייחס שינויים למשתמשים בודדים, יש להשתמש ב־Subject Claims של JWT/OIDC. סודות אינם נרשמים לעולם. קונטרולר הניהול מוסיף רשומת Change Log לצד כל שינוי.

## הרשאות ניהול והרשאות Proxy

קונטרולר ביצוע ה־Proxy מוגן באמצעות `ProxyApiKeyAuthorizationFilter` ודורש את הכותרת `X-Proxy-Api-Key`.

קונטרולר הניהול (`OperationsAdminController`) מוגן בנפרד באמצעות `AdminApiKeyAuthorizationFilter`, המממש `IAuthorizationFilter` ודורש שהכותרת `X-Admin-Api-Key` תתאים לערך `Admin:ApiKey` שמגיע מתצורת הסודות.

שני הפילטרים פועלים במדיניות **Fail Closed**:

- מפתח חסר – 401.
- מפתח שגוי – 403.
- מפתח שרת שלא הוגדר – נקודת הקצה המוגנת נסגרת עם 503.

בכל אחד מהמקרים, הפעולה המוגנת אינה מתבצעת. שני המפתחות נקראים מתצורת סודות (ולא מקובץ `appsettings` שנשמר במאגר), וההשוואה מתבצעת באמצעות `CryptographicOperations.FixedTimeEquals` כדי להימנע מדליפות מידע דרך הבדלי זמני השוואה.

בסביבת Production יש להשתמש ב־OIDC/JWT ובהרשאות מבוססות תפקידים. מפתח הצרכן הוא שער זהות נוסף; הוא אינו מחליף את תהליך המדיניות של הפעולה, שקובע מה מותר להעביר לשירות החיצוני.

ניסיונות Proxy עם מפתח צרכן חסר או שגוי נרשמים כאירועי אבטחה מובנים יחד עם Correlation ID. מכיוון שהבקשה נדחית לפני איתור הפעולה, לא נוצרת עבורה רשומת ביצוע ב־`RequestLogs`.

## Allowlist למתודות HTTP

מתודת ה־HTTP לשירות החיצוני מגיעה מהתצורה, ו־`ProxyExecutor` בודק אותה מול Allowlist:

`GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`

ערך לא מוכר שהוגדר בתצורה נדחה, במקום להישלח כפי שהוא. כך ה־Proxy אינו יכול להעביר לשירות החיצוני מתודה שרירותית.

## אסטרטגיית בדיקות

- **Unit/Integration:** בדיקות של תהליך אמיתי באמצעות SQLite in-memory ו־Recording HTTP Handler. `CachedConfigurationProvider` פועל מול `OperationRepository` אמיתי שמחובר ל־SQLite.
- **End-to-End:** שימוש ב־`WebApplicationFactory<Program>` לבדיקת Controller, Model Binding, אכיפת Content-Type וכתיבת תגובות. הבדיקות כוללות גם תהליכי Admin שמפעילים את מסלול Repository ו־Unit of Work ואת מנגנון 409 של Optimistic Concurrency.
- **בדיקות דחייה:** מוודאות שה־Recording Handler קיבל אפס בקשות. כך מוכח שהבקשה נחסמה לפני ההעברה, ולא רק שהוחזר קוד סטטוס מסוים.

## פרופילי אימות מול שירותים חיצוניים

`Destination.AuthProfileRef` הוא הפניה אטומה לפרופיל, ולא חומר סודי עצמו.

מימוש ההערכה תומך ב־Bearer Tokens שמופנים באמצעות `env:VARIABLE_NAME` או `config:ProfileName` (נתיב התצורה: `UpstreamAuth:Profiles:ProfileName`). הספק מוסיף את header `Authorization` רק לאחר סינון הheaders הנכנסים . לכן מפתח ה־API של הצרכן וכל header Authorization שהלקוח ניסה לשלוח אינם מועברים לשירות החיצוני.

אם הפרופיל חסר, לא ניתן לפתור אותו או שהוא אינו תקין, מוחזרת השגיאה `503 upstream_auth_unavailable` והבקשה אינה נשלחת. מנגנוני אימות אחרים (mTLS, HMAC או כותרות API Key) צריכים להיות ממומשים כספקים ייעודיים ומטיפוסים נפרדים, ולא באמצעות העמסה לא מפורשת של פרופיל ה־Bearer הקיים.

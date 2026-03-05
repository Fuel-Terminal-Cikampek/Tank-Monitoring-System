# 📋 IMPLEMENTATION GUIDE: Tank_LiveDataTMS

## 🎯 TUJUAN
Membuat table **Tank_LiveDataTMS** yang CLEAN (tanpa kolom legacy) khusus untuk TMS Cikampek, agar **Tank_LiveData** tetap compatible dengan sistem lama (FDM Web).

---

## 📊 RINGKASAN PERUBAHAN

### **Table Baru:**
- **Tank_LiveDataTMS** (19 kolom CLEAN - tanpa 9 kolom legacy)

### **Kolom yang DIHAPUS dari Tank_LiveDataTMS:**
1. `Ack` (BIT)
2. `FlowRateMperSecond` (FLOAT)
3. `LastLiquidLevel` (FLOAT)
4. `LastTimeStamp` (DATETIME)
5. `TotalSecond` (INT)
6. `LastVolume` (FLOAT)
7. `Pumpable` (FLOAT)
8. `AlarmMessage` (VARCHAR)
9. `Ullage` (FLOAT)

### **Files Modified:**
1. ✅ **TMS.Models/TankLiveDataTMS.cs** - NEW model (19 kolom)
2. ✅ **TMS.Models/TankLiveData.cs** - NotMapped 9 kolom legacy
3. ✅ **TMS.Web/Areas/Identity/Data/TMSContext.cs** - Add DbSet + config
4. ✅ **TMS.ATGService/Service/AppDbContext.cs** - Add DbSet + config
5. ✅ **TMS.ATGService/Service/DBHelper.cs** - Add method `updateTankLiveDataTMS()`
6. ✅ **TMS.ATGService/WorkerXmlMode.cs** - Call new method
7. ✅ **Database/Create_Tank_LiveDataTMS.sql** - SQL script untuk CREATE TABLE + backfill

---

## 🚀 STEP-BY-STEP IMPLEMENTATION

### ✅ FASE 1: CODE CHANGES (SUDAH SELESAI)

Semua perubahan kode sudah dilakukan:

1. **Model TankLiveDataTMS** - Created ✅
2. **Model TankLiveData** - NotMapped 9 kolom ✅
3. **DbContext (Web)** - Updated ✅
4. **DbContext (ATG Service)** - Updated ✅
5. **DBHelper** - Method baru added ✅
6. **WorkerXmlMode** - Call method baru ✅

---

### ⏳ FASE 2: DATABASE CHANGES (BELUM DILAKUKAN - TUNGGU TESTING)

**URUTAN EKSEKUSI:**

#### **STEP 1: BUILD SOLUTION** 🔨

```bash
# Build solution untuk ensure no compilation errors
dotnet build TMS.sln
```

**Expected Output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**⚠️ JIKA ADA ERROR:**
- Error terkait TankLiveDataTMS → Check using statement di file yang error
- Add: `using TMS.Models;` jika belum ada

---

#### **STEP 2: TEST ATG SERVICE (LOCAL - DRY RUN)** 🧪

**Purpose:** Test kode baru TANPA execute SQL script dulu

**2A. Set Connection String di appsettings.json (ATG Service)**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=192.168.2.3;Database=TAS_CIKAMPEK2025;User ID=sa;Password=p3rt4m1n@;Integrated Security=False;"
  }
}
```

**2B. Run ATG Service (Dry Run)**

```bash
cd TMS.ATGService
dotnet run --configuration Debug
```

**Expected Behavior:**
- ✅ Service starts successfully
- ✅ Console shows: `[DB] ✅ Updated Tank=T-1, Level=XXXmm`
- ⚠️ Console shows: `⚠️ WARNING: Failed to update Tank_LiveDataTMS (T-1): Invalid object name 'Tank_LiveDataTMS'.`

**Ini NORMAL!** Error karena table Tank_LiveDataTMS belum dibuat. Tapi:
- ✅ Tank_LiveData tetap update (non-blocking)
- ✅ Error di-catch dan di-log (tidak crash)

**2C. Stop Service**
- Press `Ctrl+C` to stop

---

#### **STEP 3: EXECUTE SQL SCRIPT** 💾

**⚠️ BACKUP DATABASE FIRST!**

```sql
-- Backup database
BACKUP DATABASE TAS_CIKAMPEK2025
TO DISK = 'C:\Backup\TAS_CIKAMPEK2025_Before_Tank_LiveDataTMS.bak'
WITH FORMAT, INIT, NAME = 'Before Tank_LiveDataTMS';
```

**Execute Script:**

1. Open SSMS (SQL Server Management Studio)
2. Connect to: `192.168.2.3` dengan user `sa`
3. Open file: `Database\Create_Tank_LiveDataTMS.sql`
4. Execute (F5)

**Expected Output:**
```
✅  Table Tank_LiveDataTMS created successfully!
    Columns: 19 (CLEAN - no legacy columns)
✅  Index IX_Tank_LiveDataTMS_TimeStamp created
✅  Index IX_Tank_LiveDataTMS_Product_ID created
✅  Index IX_Tank_LiveDataTMS_Tank_TimeStamp created
✅  Data backfilled successfully!
    Rows copied: XXX
✅  Row counts match!
```

**Verification Query:**
```sql
-- Check table structure
SELECT * FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Tank_LiveDataTMS'
ORDER BY ORDINAL_POSITION

-- Check row count
SELECT COUNT(*) as TotalRows FROM Tank_LiveDataTMS

-- Compare with Tank_LiveData
SELECT
    (SELECT COUNT(*) FROM Tank_LiveData) as LiveData_Count,
    (SELECT COUNT(*) FROM Tank_LiveDataTMS) as TMS_Count
```

---

#### **STEP 4: TEST ATG SERVICE (WITH DATABASE)** ✅

**Run ATG Service Again:**

```bash
cd TMS.ATGService
dotnet run --configuration Debug
```

**Expected Behavior:**
- ✅ Service starts successfully
- ✅ Console shows: `[DB] ✅ Updated Tank=T-1, Level=XXXmm`
- ✅ Console shows: `[DB-TMS] ✅ Updated Tank=T-1, Level=XXXmm`
- ✅ NO more errors about Tank_LiveDataTMS

**Monitor Logs:**
```
[DB] Tank=T-1, Level=1234mm, Existing=1200mm
[DB] ✅ Updated Tank=T-1, Level=1234mm
[DB-TMS] Tank=T-1, Level=1234mm, Existing=1200mm
[DB-TMS] ✅ Updated Tank=T-1, Level=1234mm
[T-1] Flowrate = 12.50 KL/h
```

**Let it run for 2-3 cycles** (~30 seconds) untuk ensure data sync properly.

---

#### **STEP 5: VERIFICATION QUERIES** 🔍

**5A. Check Data Sync:**

```sql
-- Compare latest data between tables
SELECT
    ld.Tank_Number,
    ld.Level as LiveData_Level,
    tms.Level as TMS_Level,
    CASE
        WHEN ld.Level = tms.Level THEN '✅ MATCH'
        ELSE '❌ MISMATCH'
    END as Status,
    ld.Flowrate as LiveData_Flowrate,
    tms.Flowrate as TMS_Flowrate,
    ld.TimeStamp as LiveData_Time,
    tms.TimeStamp as TMS_Time
FROM Tank_LiveData ld
LEFT JOIN Tank_LiveDataTMS tms ON ld.Tank_Number = tms.Tank_Number
ORDER BY ld.Tank_Number
```

**5B. Check Column Differences:**

```sql
-- Show columns in Tank_LiveData
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Tank_LiveData'
ORDER BY ORDINAL_POSITION

-- Show columns in Tank_LiveDataTMS
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Tank_LiveDataTMS'
ORDER BY ORDINAL_POSITION

-- Difference:
-- Tank_LiveData has 28 columns
-- Tank_LiveDataTMS has 19 columns (missing 9 legacy columns)
```

**5C. Check Update Frequency:**

```sql
-- Monitor real-time updates (run in loop)
WAITFOR DELAY '00:00:10'  -- Wait 10 seconds
SELECT
    Tank_Number,
    Level,
    Flowrate,
    TimeStamp
FROM Tank_LiveDataTMS
ORDER BY Tank_Number
GO 5  -- Repeat 5 times
```

---

#### **STEP 6: TEST TMS.WEB** 🌐

**6A. Build Web Application:**

```bash
cd TMS.Web
dotnet build
```

**6B. Run Web Application:**

```bash
dotnet run --configuration Debug
```

**6C. Test UI:**

1. Navigate to: `https://localhost:5001/TankLiveDatas`
2. Check Tank Live Data page loads without errors
3. Verify data displays correctly
4. Check console for any errors

**Expected:**
- ✅ Page loads successfully
- ✅ Data displays (from Tank_LiveData)
- ✅ No errors about missing columns

---

## 🧪 TESTING CHECKLIST

### ✅ **Unit Tests:**

- [ ] ATG Service starts without errors
- [ ] Tank_LiveData updates successfully
- [ ] Tank_LiveDataTMS updates successfully
- [ ] Error handling works (non-blocking on TMS update failure)
- [ ] Data sync between tables

### ✅ **Integration Tests:**

- [ ] TMS.Web connects to database
- [ ] TankLiveDatas/Index page loads
- [ ] No errors in browser console
- [ ] No errors in application logs

### ✅ **Performance Tests:**

- [ ] ATG Service polling cycle time < 10 seconds
- [ ] Database update time reasonable
- [ ] No significant performance degradation

---

## 📊 MONITORING

### **Key Metrics to Monitor:**

1. **Row Count Consistency:**
   ```sql
   SELECT
       (SELECT COUNT(*) FROM Tank_LiveData) as LiveData,
       (SELECT COUNT(*) FROM Tank_LiveDataTMS) as TMS,
       (SELECT COUNT(*) FROM Tank_LiveData) -
       (SELECT COUNT(*) FROM Tank_LiveDataTMS) as Difference
   ```

2. **Last Update Time:**
   ```sql
   SELECT
       MAX(TimeStamp) as LiveData_LastUpdate
   FROM Tank_LiveData

   SELECT
       MAX(TimeStamp) as TMS_LastUpdate
   FROM Tank_LiveDataTMS
   ```

3. **Data Freshness:**
   ```sql
   -- Should be < 1 minute
   SELECT
       Tank_Number,
       TimeStamp,
       DATEDIFF(SECOND, TimeStamp, GETDATE()) as Seconds_Ago
   FROM Tank_LiveDataTMS
   ORDER BY TimeStamp DESC
   ```

---

## ⚠️ TROUBLESHOOTING

### **Error: "Invalid object name 'Tank_LiveDataTMS'"**

**Solution:**
- Execute SQL script `Create_Tank_LiveDataTMS.sql`
- Verify table exists: `SELECT * FROM Tank_LiveDataTMS`

---

### **Error: "Cannot insert duplicate key"**

**Solution:**
- Table already has data with same Tank_Number
- Use UPDATE instead of INSERT
- Check DBHelper logic (should auto-detect existing records)

---

### **Error: "Invalid column name 'Ack'"**

**Solution:**
- TankLiveData model masih ada [Column("Ack")] yang tidak di-comment
- Ensure [Column] attribute di-comment dan [NotMapped] sudah ditambahkan

---

### **Data not syncing between tables:**

**Solution:**
1. Check ATG Service logs untuk errors
2. Verify DBHelper.updateTankLiveDataTMS() dipanggil
3. Check database permissions
4. Verify connection string correct

---

## 🎯 SUCCESS CRITERIA

Implementation dianggap **SUKSES** jika:

1. ✅ ATG Service runs tanpa errors
2. ✅ Tank_LiveData updates normally (existing functionality)
3. ✅ Tank_LiveDataTMS updates in parallel (new functionality)
4. ✅ Row counts match antara kedua tables
5. ✅ Data sync dengan latency < 1 menit
6. ✅ TMS.Web berfungsi normal (no breaking changes)
7. ✅ No performance degradation

---

## 📝 ROLLBACK PLAN

Jika terjadi masalah CRITICAL:

### **ROLLBACK STEP 1: Revert Code Changes**

```bash
git checkout HEAD -- TMS.Models/TankLiveData.cs
git checkout HEAD -- TMS.ATGService/Service/DBHelper.cs
git checkout HEAD -- TMS.ATGService/WorkerXmlMode.cs
```

### **ROLLBACK STEP 2: Drop Table**

```sql
USE TAS_CIKAMPEK2025
GO

DROP TABLE IF EXISTS Tank_LiveDataTMS
GO
```

### **ROLLBACK STEP 3: Restore Backup**

```sql
RESTORE DATABASE TAS_CIKAMPEK2025
FROM DISK = 'C:\Backup\TAS_CIKAMPEK2025_Before_Tank_LiveDataTMS.bak'
WITH REPLACE
GO
```

---

## 📞 SUPPORT

Jika ada masalah atau pertanyaan:

1. Check logs di: `TMS.ATGService/Logs/`
2. Check SQL Server error logs
3. Review this implementation guide
4. Contact: [Your Contact Info]

---

## 🎉 NEXT STEPS (AFTER SUCCESS)

1. Monitor system for 24-48 hours
2. Verify data consistency daily
3. Plan untuk DROP 9 kolom legacy dari Tank_LiveData (future - setelah FDM Web tidak dipakai lagi)
4. Setup automated monitoring/alerting
5. Document lessons learned

---

**Last Updated:** 2026-02-02
**Author:** Claude Sonnet 4.5
**Status:** Ready for Testing

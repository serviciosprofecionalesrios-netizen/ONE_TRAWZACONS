using ITServiceDeskApp.Controllers;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeOpenXml;
using System.Security.Claims;

void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
ApplicationDbContext Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
using var db = Database();
OperationalMasterCatalog.Seed(db);
var originalIds = db.OperationalMasterRecords.ToDictionary(x=>x.Id,x=>x.Name);
Check(originalIds.Count==111 && db.OperationalMasterRecords.All(x=>x.CompanyClassification=="EQUINOX"),"Original masters remain EQUINOX");
var sources = HolcimMasterCatalog.ReadSeed();
Check(sources.Count(x=>x.Kind=="equipos")==12 && sources.Count(x=>x.Kind=="conductores")==14 && sources.All(x=>x.Kind!="gondolas"),"12 equipment and 14 unique drivers; no invented gondolas");
Check(sources.Sum(x=>x.Detail.Rows.Count)==54 && sources.Where(x=>x.Kind=="conductores").All(x=>x.Detail.Rows.Count==3),"All 54 source rows preserved, training consolidated per driver");
var imported=HolcimMasterCatalog.Seed(db);
Check(imported.Added==26 && imported.Linked==0 && imported.Ambiguous==0 && db.OperationalMasterRecords.Count()==137,"Initial catalog imports all supplied records once");
Check(originalIds.All(x=>db.OperationalMasterRecords.Find(x.Key)!.Name==x.Value),"Existing IDs and identities preserved");
Check(db.OperationalMasterRecords.Where(x=>x.HolcimReference!=null).All(x=>x.CompanyClassification=="HOLCIM" && x.Tenure=="Pendiente de completar" && x.Owner=="Pendiente de completar"),"Missing owner/tenure kept pending; company is not ownership");
Check(db.OperationalMasterRecords.Where(x=>x.HolcimReference!=null && x.Kind=="equipos").All(x=>x.Plate==null),"Source reference is not invented as a confirmed registration plate");
var sample=db.OperationalMasterRecords.Single(x=>x.HolcimReference=="N02900002H");
var detail=HolcimMasterCatalog.Detail(sample.HolcimSourceJson)!;
Check(detail.File=="Holcim NSC.xlsx" && detail.Rows.Single(x=>x.Values["Código"]=="802").Values["Válido hasta"]=="2027-08-07 00:00:00","Training details and exact validity dates retained");
sample.Name="Nombre corregido por administración";sample.RecordKey=OperationalMasterCatalog.Key(sample.Kind,sample.Name);sample.IsActive=false;db.SaveChanges();
HolcimMasterCatalog.Seed(db);OperationalMasterCatalog.Seed(db);
Check(db.OperationalMasterRecords.Count()==137 && !sample.IsActive && sample.Name=="Nombre corregido por administración","Restart does not duplicate, resurrect or overwrite imported identities");

using var shared = Database();
var driver=new OperationalMasterRecord{Kind="conductores",Name="  Deyvi José Guerrero Rivas ",Tenure="Propio",Owner="TRAWZACONS",RecordKey="original",IsActive=false};
var truck=new OperationalMasterRecord{Kind="equipos",Name="W-0500",Plate="M-268282",Tenure="Subcontratado",Owner="Existing owner",RecordKey="truck"};
var gondola=new OperationalMasterRecord{Kind="gondolas",Name="NM120616",Tenure="Propio",Owner="Trailer owner",RecordKey="trailer"};
shared.AddRange(driver,truck,gondola);shared.SaveChanges();var driverId=driver.Id;var truckId=truck.Id;
var linked=HolcimMasterCatalog.Seed(shared);
Check(linked.Linked==2 && shared.OperationalMasterRecords.Count()==27 && driver.Id==driverId && truck.Id==truckId,"Shared driver full name and equipment plate keep their original IDs");
Check(driver.CompanyClassification=="HOLCIM/EQUINOX" && truck.CompanyClassification=="HOLCIM/EQUINOX" && gondola.CompanyClassification=="EQUINOX","Both-company classification is kind-specific, including inactive shared drivers");
Check(driver.Owner=="TRAWZACONS" && !driver.IsActive && truck.Owner=="Existing owner" && truck.Plate=="M-268282" && truck.Name=="W-0500","Linking preserves ownership, state and equipment code/plate");
Check(shared.OperationalMasterRecords.Count(x=>x.HolcimReference=="NM120616")==1,"Matching trailer code does not swallow equipment record");
Check(HolcimMasterCatalog.Matches([new OperationalMasterRecord{Kind="conductores",Name="Deyvi Guerrero"}],sources.Single(x=>x.Reference=="N06860035L")).Count==0,"Partial driver names never auto-merge");
using var ambiguous = Database();
ambiguous.AddRange(new OperationalMasterRecord{Kind="equipos",Name="NM120616",RecordKey="a",Tenure="Propio",Owner="A"},new OperationalMasterRecord{Kind="equipos",Name="Other",Plate="NM120616",RecordKey="b",Tenure="Propio",Owner="B"});ambiguous.SaveChanges();
var conflict=HolcimMasterCatalog.Seed(ambiguous,sources:sources.Where(x=>x.Reference=="NM120616"));
Check(conflict.Ambiguous==1 && ambiguous.OperationalMasterRecords.Count()==2 && ambiguous.OperationalMasterRecords.All(x=>x.CompanyClassification=="EQUINOX"),"Ambiguous matches neither merge nor duplicate");

OperacionesController Controller(ApplicationDbContext context,string email="admin@trawzacons.com") {
 var http=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email,email),new Claim(ClaimTypes.Role,"Administrator")],"test"))};
 return new OperacionesController(null!,null!,null!,null!,null!,null!,context,null!,null!,NullLogger<OperacionesController>.Instance,null!,null!){ControllerContext=new ControllerContext{HttpContext=http},TempData=new TempDataDictionary(http,new TestTempDataProvider())};
}
IReadOnlyList<OperationalMasterRecord> Results(IActionResult result)=>(IReadOnlyList<OperationalMasterRecord>)((ViewResult)result).Model!;
Check(Results(await Controller(shared).Maestros(type:"conductores",status:"todos",company:"HOLCIM/EQUINOX")).Single().Id==driverId,"Shared-only filter");
Check(Results(await Controller(shared).Maestros(type:"equipos",company:"EQUINOX")).Any(x=>x.Id==truckId) && Results(await Controller(shared).Maestros(type:"equipos",company:"HOLCIM")).Any(x=>x.Id==truckId),"Each company filter includes shared records");
Check(await Controller(shared).Maestros(company:"Unknown") is BadRequestResult,"Invalid company filter rejected");
var bad=Controller(shared);Check(await bad.MaestroNuevo(new OperationalMasterRecord{Kind="equipos",Name="Unique",Plate="X123",Tenure="Propio",Owner="Owner",CompanyClassification="Fake"},default) is ViewResult && !bad.ModelState.IsValid,"Server rejects invalid manual company");
Check(await Controller(shared,"leslieantonioespinoza@trawzacons.com").MaestroImportar("conductores",null,default) is ForbidResult,"Import does not widen master-maintenance permissions");

// Exercise generated template, real import, company union and atomic invalid batches.
using var bulkDb=Database();
bulkDb.OperationalMasterRecords.Add(new OperationalMasterRecord{Kind="conductores",Name="Juan Carlos Perez Lopez",RecordKey="conductores:JUAN CARLOS PEREZ LOPEZ",Tenure="Propio",Owner="Keep",IsActive=false});bulkDb.SaveChanges();
var template=(FileContentResult)Controller(bulkDb).MaestroPlantilla("conductores");
ExcelPackage.LicenseContext=LicenseContext.NonCommercial;
using var excel=new ExcelPackage(new MemoryStream(template.FileContents));var sheet=excel.Workbook.Worksheets[0];
Check(sheet.Cells[1,8].Text==HolcimMasterCatalog.CompanyColumn,"Template exposes company classification");
var values=new[]{"Juan","Carlos","Perez","Lopez","Propio","Don't overwrite","HOLCIM"};
for(var c=0;c<values.Length;c++)sheet.Cells[2,c+2].Value=values[c];
IFormFile Upload(byte[] bytes)=>new FormFile(new MemoryStream(bytes),0,bytes.Length,"archivo","maestros.xlsx");
sheet.Cells[3,2].Value="Incomplete";
var rejected=Controller(bulkDb);await rejected.MaestroImportar("conductores",Upload(excel.GetAsByteArray()),default);
Check(rejected.TempData.ContainsKey("MasterError") && bulkDb.OperationalMasterRecords.Single().CompanyClassification=="EQUINOX","Invalid batch cannot partially change existing classification");
sheet.DeleteRow(3);await Controller(bulkDb).MaestroImportar("conductores",Upload(excel.GetAsByteArray()),default);
Check(bulkDb.OperationalMasterRecords.Count()==1 && bulkDb.OperationalMasterRecords.Single().CompanyClassification=="HOLCIM/EQUINOX" && bulkDb.OperationalMasterRecords.Single().Owner=="Keep" && !bulkDb.OperationalMasterRecords.Single().IsActive,"Bulk merges company without duplicating or changing other master information");
await Controller(bulkDb).MaestroImportar("conductores",Upload(excel.GetAsByteArray()),default);
Check(bulkDb.OperationalMasterRecords.Count()==1,"Repeat company import is idempotent");
sheet.DeleteColumn(8);for(var c=0;c<6;c++)sheet.Cells[2,c+2].Value=new[]{"Pedro","Miguel","Gonzalez","Ruiz","Propio","Owner"}[c];
await Controller(bulkDb).MaestroImportar("conductores",Upload(excel.GetAsByteArray()),default);
Check(bulkDb.OperationalMasterRecords.Count()==2 && bulkDb.OperationalMasterRecords.Single(x=>x.Name.StartsWith("Pedro")).CompanyClassification=="EQUINOX","Previous template remains compatible and defaults to EQUINOX");
var trailer=new OperationalMasterRecord{Kind="gondolas",Name="M330-857",RecordKey="gondolas:M330-857",Tenure="Propio",Owner="Keep trailer"};bulkDb.Add(trailer);bulkDb.SaveChanges();
var trailerTemplate=(FileContentResult)Controller(bulkDb).MaestroPlantilla("gondolas");
using(var trailerExcel=new ExcelPackage(new MemoryStream(trailerTemplate.FileContents))) {
 var tab=trailerExcel.Workbook.Worksheets[0];var trailerValues=new[]{"Propio","Do not overwrite","M330857","HOLCIM"};
 for(var c=0;c<trailerValues.Length;c++)tab.Cells[2,c+2].Value=trailerValues[c];
 await Controller(bulkDb).MaestroImportar("gondolas",Upload(trailerExcel.GetAsByteArray()),default);
 Check(bulkDb.OperationalMasterRecords.Count(x=>x.Kind=="gondolas")==1 && trailer.CompanyClassification=="HOLCIM/EQUINOX" && trailer.Owner=="Keep trailer","Shared gondola classification ignores plate spacing/hyphens and keeps one ID");
}
using var pg=new PostgresApplicationDbContext(new DbContextOptionsBuilder<PostgresApplicationDbContext>().UseNpgsql("Host=localhost;Database=check;Username=check;Password=unused").Options);
var pgScript=pg.GetService<IMigrator>().GenerateScript("20261022010000_SiteActivityPostgres","20261027010000_MasterCompanyClassificationPostgres");
Check(pgScript.Contains("DEFAULT 'EQUINOX'") && pgScript.Contains("HolcimSourceJson"),"PostgreSQL upgrade classifies existing masters and stores source detail");
using var sql=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=localhost;Database=check;Integrated Security=True;TrustServerCertificate=True").Options);
Check(sql.GetService<IMigrator>().GenerateScript("20261022010000_SiteActivity","20261027010000_MasterCompanyClassification").Contains("EQUINOX"),"SQL Server upgrade generated");
Console.WriteLine("PASS: Holcim source completeness, company classification, safe shared matches, immutable IDs, restart, imports, permissions and migrations.");
sealed class TestTempDataProvider : ITempDataProvider {
 public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();
 public void SaveTempData(HttpContext context,IDictionary<string,object> values){}
}

<?php
ob_start();session_start();
class Database {static $db;static function GetInstance(){return self::$db;}}
class Functions {static $authenticated=true;static function IsLoggedIn(){return self::$authenticated;}static function GetPlayer(){return ['userId'=>987654321];}}
require 'G:/xpp/htdocs/files/classes/AccountLanguage.php';
$db=Database::$db=new mysqli('127.0.0.1','root','','darkorbit');
$db->query("CREATE TEMPORARY TABLE player_accounts (userId INT PRIMARY KEY, language VARCHAR(5) NOT NULL DEFAULT 'pt')");
$db->query("INSERT INTO player_accounts (userId) VALUES (987654321),(987654322)");
function ensure($condition,$text){if(!$condition)throw new Exception($text);}
$token=AccountLanguage::Token();
foreach(['en','es','de','pt'] as $code){
 $result=AccountLanguage::Save($code,$token);ensure($result['status'],'Saving failed');
 $row=$db->query('SELECT language FROM player_accounts WHERE userId=987654321')->fetch_assoc();ensure($row['language']===$code,'Not persisted');
 $row=$db->query('SELECT language FROM player_accounts WHERE userId=987654322')->fetch_assoc();ensure($row['language']==='pt','Changed another account');
 ensure(AccountLanguage::Number(1234567.5,2)===($code==='en'?'1,234,567.50':'1.234.567,50'),'Incorrect site format');
}
ensure(!AccountLanguage::Save('en','wrong-token')['status'],'Invalid CSRF accepted');
ensure(!AccountLanguage::Save('../en',$token)['status'],'Invalid locale accepted');
Functions::$authenticated=false;ensure(!AccountLanguage::Save('en',$token)['status'],'Anonymous change accepted');
echo "Account language: database persistence, account isolation, locale allowlist, CSRF, authentication and site number formatting passed. Temporary tables only.\n";

<?php
// Exercise registration against connection-local temporary tables; no mail or real accounts.
ob_start(); session_start();
define('DOMAIN', 'http://localhost/'); define('SERVER_NAME','Test');
$_SERVER['REMOTE_ADDR']='127.0.0.1';
class SMTP { static function SendMail(...$args) {} }
class Connection { public $db; function __construct(){ $this->db=new mysqli('127.0.0.1','root','','darkorbit'); } function __call($name,$args){return $this->db->$name(...$args);} function __get($name){return $this->db->$name;} function close(){} }
class Database { static $db; static function GetInstance(){return self::$db;} }
Database::$db = new Connection();
$db=Database::$db;
foreach (['player_accounts','player_equipment','player_settings','player_titles','player_inventory_items','player_inventory_loadout'] as $table) {
  $ddl=$db->query('SHOW CREATE TABLE '.$table)->fetch_row()[1];
  $db->query(preg_replace('/^CREATE TABLE/', 'CREATE TEMPORARY TABLE', $ddl));
}
require 'G:/xpp/htdocs/files/classes/Functions.php';
$result=json_decode(Functions::Register('starter_qa','testpass123','testpass123','starter@example.invalid'),true);
if (!$result['status']) throw new Exception('Registration failed');
$id=(int)$_SESSION['account']['id'];
$account=$db->query('SELECT * FROM player_accounts WHERE userId='.$id)->fetch_assoc();
$ammo=json_decode($account['ammo'],true);
if ((int)$account['shipId']!==1 || $ammo['lcb10']!==1000 || $ammo['r310']!==100 || array_sum($ammo)!==1100) throw new Exception('Unexpected starter ship/ammo');
$items=$db->query('SELECT item_id FROM player_inventory_items WHERE userId='.$id.' ORDER BY item_id')->fetch_all(MYSQLI_NUM);
if (array_column($items,0)!==['43','124','131']) throw new Exception('Unexpected inventory');
foreach ([1,2] as $cfg) {
  $slots=$db->query('SELECT slot_group,item_id FROM player_inventory_loadout WHERE userId='.$id.' AND config_id='.$cfg.' ORDER BY item_id')->fetch_all(MYSQLI_NUM);
  if ($slots!==[['cpus','43'],['lasers','124'],['generators','131']]) throw new Exception('Unexpected equipment');
}
echo "Starter registration passed: Phoenix, exact ammunition, MP-1/A01/REP-S, both configs, automatic session. Temporary tables only; mail disabled.\n";

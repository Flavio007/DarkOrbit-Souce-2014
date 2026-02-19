RocketLauncherAssembly is a singleton class that manages the rocket launcher configurations and effects in the game. It listens for specific packets related to rocket launcher updates and applies the corresponding changes to the player's settings and visual effects.

package net.bigpoint.darkorbit.net
{
   import _SafePkg_100._SafeCls_569;
   import _SafePkg_139._SafeCls_568;
   import _SafePkg_142._SafeCls_570;
   import _SafePkg_114._SafeCls_113;
   import _SafePkg_118._SafeCls_119;
   import flash.utils.Dictionary;
   import net.bigpoint.darkorbit._SafeCls_21;
   import net.bigpoint.darkorbit._SafeCls_176;
   import net.bigpoint.darkorbit.settings.Settings;
   
   public class _SafeCls_173 extends _SafeCls_135
   {
      
      private static var instance:_SafeCls_173;
      
      public var _SafeStr_10698:Dictionary;
      
      private var _SafeStr_3614:Dictionary;
      
      private var main:_SafeCls_21;
      
      public function _SafeCls_173(param1:Function)
      {
         super();
         if(param1 !== _SafeStr_4070)
         {
            throw new Error("RocketLauncherAssembly is a Singleton and can only be accessed through RocketLauncherAssembly.getInstance()");
         }
         this.main = _SafeStr_4896;
         this._SafeStr_4897();
         this._SafeStr_10997();
      }
      
      public static function getInstance() : _SafeCls_173
      {
         if(instance == null)
         {
            instance = new _SafeCls_173(_SafeStr_4070);
         }
         return instance;
      }
      
      private static function _SafeStr_4070() : void
      {
      }
      
      private function _SafeStr_4897() : void
      {
         this._SafeStr_3614 = new Dictionary();
         this._SafeStr_3614[_SafeCls_101._SafeStr_17826] = this._SafeStr_3398;
         this._SafeStr_3614[_SafeCls_101._SafeStr_10833] = this._SafeStr_3398;
         this._SafeStr_3614[_SafeCls_101._SafeStr_10271] = this._SafeStr_7792;
         this._SafeStr_3614[_SafeCls_101._SafeStr_7588] = this._SafeStr_7792;
      }
      
      private function _SafeStr_10997() : void
      {
         this._SafeStr_10698 = new Dictionary();
         this._SafeStr_10698[_SafeCls_570._SafeStr_4926] = _SafeCls_569._SafeStr_13949;
         this._SafeStr_10698[_SafeCls_570._SafeStr_5846] = _SafeCls_569._SafeStr_12383;
         this._SafeStr_10698[_SafeCls_570._SafeStr_5806] = _SafeCls_569._SafeStr_17910;
         this._SafeStr_10698[_SafeCls_570.SAR01] = _SafeCls_569._SafeStr_4941;
         this._SafeStr_10698[_SafeCls_570.SAR02] = _SafeCls_569._SafeStr_11690;
         this._SafeStr_10698[_SafeCls_570.CBR] = _SafeCls_569.CBR;
         this._SafeStr_10698[_SafeCls_570._SafeStr_4870] = _SafeCls_569._SafeStr_9866;
      }
      
      public function _SafeStr_14980(param1:Array) : void
      {
         var _loc2_:String = param1[2];
         if(this._SafeStr_3614[_loc2_] != null)
         {
            this._SafeStr_3614[_loc2_](param1);
         }
      }
      
      public function _SafeStr_7792(param1:Array) : void
      {
         var _loc7_:_SafeCls_119 = null;
         var _loc8_:_SafeCls_119 = null;
         var _loc2_:int = int(param1[3]);
         var _loc3_:int = int(param1[4]);
         var _loc4_:int = int(param1[5]);
         var _loc5_:int = int(param1[6]);
         var _loc6_:Boolean = false;
         if(param1[7] == "M")
         {
            _loc6_ = true;
         }
         if(_SafeStr_4896._SafeStr_6579.map != null)
         {
            _loc7_ = map._SafeStr_4053(_loc2_);
            _loc8_ = map._SafeStr_4053(_loc3_);
            if(Boolean(_loc7_) && Boolean(_loc8_))
            {
               _SafeStr_4896._SafeStr_6579.map.effects._SafeStr_14987(new _SafeCls_568(_loc7_,_loc8_,_loc5_,_loc4_,_loc6_));
            }
         }
      }
      
      private function _SafeStr_6371(param1:Vector.<int>, param2:int, param3:int) : void
      {
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         Settings.rocketLauncherTypes = param1;
         Settings.selectedLauncherRocket = param2;
         Settings.rocketLauncherRocketsLoaded = param3;
         if(Settings.rocketLauncherTypes.length == 0 || Settings.rocketLauncherTypes[0] == 0)
         {
            Settings.rocketLauncherRocketsLoaded = 0;
         }
         else
         {
            _loc4_ = 0;
            for each(_loc5_ in Settings.rocketLauncherTypes)
            {
               if(_loc5_ == 1)
               {
                  _loc4_ += 3;
               }
               else if(_loc5_ == 2)
               {
                  _loc4_ += 5;
               }
            }
            if(Settings.rocketLauncherRocketsLoaded == 1 && Settings.rocketLauncherRocketsLoaded < _loc4_)
            {
               _SafeCls_113.playSoundEffect(_SafeCls_176._SafeStr_4956);
            }
            else if(Settings.rocketLauncherRocketsLoaded > 0 && Settings.rocketLauncherRocketsLoaded < _loc4_)
            {
               _SafeCls_113.playSoundEffect(_SafeCls_176._SafeStr_5248);
            }
            else if(Settings.rocketLauncherRocketsLoaded == _loc4_)
            {
               _SafeCls_113.playSoundEffect(_SafeCls_176._SafeStr_6521);
            }
         }
      }
      
      public function _SafeStr_3398(param1:Array) : void
      {
         var _loc2_:Vector.<int> = new Vector.<int>();
         _loc2_.push(int(param1[3]));
         this._SafeStr_6371(_loc2_,int(param1[4]),int(param1[5]));
      }
   }
}
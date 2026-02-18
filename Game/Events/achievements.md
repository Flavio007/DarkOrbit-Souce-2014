This contains the code for handling achievements in the game. It listens for server commands related to achievements and updates the achievement manager accordingly. The achievement manager is responsible for displaying the achievements in a window, updating their status, and handling user interactions with the achievement window.
This is the old version of the code, and it may not be complete or up to date with the latest changes in the game, but is readable and can be used as a reference for understanding how achievements are managed in the game.


            case ServerCommands.ACHIEVEMENTS:
               switch(_loc3_[2])
               {
                  case ServerCommands.ACHIEVEMENT_SET:
                     _loc59_ = 3;
                     while(_loc59_ < _loc3_.length - 2)
                     {
                        _loc150_ = int(_loc3_[_loc59_]);
                        _loc151_ = Boolean(int(_loc3_[_loc59_ + 1]));
                        _loc152_ = int(_loc3_[_loc59_ + 2]);
                        this.main.achievementManager.setAchievement(_loc150_,_loc151_,_loc152_);
                        _loc59_ += 3;
                     }
                     break;
                  case ServerCommands.ACHIEVEMENT_REMOVE:
                     _loc59_ = 3;
                     while(_loc59_ < _loc3_.length)
                     {
                        _loc150_ = int(_loc3_[_loc59_]);
                        this.main.achievementManager.removeAchievement(_loc150_);
                        _loc59_++;
                     }
                     break;
                  case ServerCommands.ACHIEVEMENT_END:
                     this.main.achievementManager.removeAchievementWindow();
               }


               package net.bigpoint.darkorbit.achievement
{
   import com.bigpoint.utils.BPLocale;
   import com.greensock.TweenLite;
   import com.greensock.TweenMax;
   import fl.containers.ScrollPane;
   import flash.display.Bitmap;
   import flash.events.Event;
   import flash.events.IOErrorEvent;
   import flash.events.MouseEvent;
   import flash.geom.Rectangle;
   import flash.net.URLLoader;
   import flash.net.URLRequest;
   import flash.text.AntiAliasType;
   import flash.text.TextField;
   import flash.text.TextFieldAutoSize;
   import flash.text.TextFormat;
   import flash.text.TextFormatAlign;
   import mx.logging.ILogger;
   import mx.logging.Log;
   import net.bigpoint.darkorbit.ResourceManager;
   import net.bigpoint.darkorbit.Styles;
   import net.bigpoint.darkorbit.audio.AudioManager;
   import net.bigpoint.darkorbit.gui.GuiManager;
   import net.bigpoint.darkorbit.gui.container.SimpleContainer;
   import net.bigpoint.darkorbit.gui.windows.SimpleWindow;
   import net.bigpoint.darkorbit.pattern.AchievementPattern;
   import net.bigpoint.darkorbit.pattern.PatternManager;
   import net.bigpoint.darkorbit.settings.Settings;
   
   public class AchievementManager
   {
      
      public static const logger:ILogger = Log.getLogger("AchievementManager");
      
      private var simpleContainer:SimpleContainer;
      
      private var scrollPane:ScrollPane;
      
      private var guiManager:GuiManager;
      
      public var achievements:Array = [];
      
      private var order:int = 0;
      
      private var updateBufferList:Array = [];
      
      private var scrollPanePaddingY:int = 0;
      
      public function AchievementManager(param1:GuiManager)
      {
         super();
         this.guiManager = param1;
      }
      
      public function getOrder() : int
      {
         return this.order++;
      }
      
      public function removeAchievementWindow() : void
      {
         var _loc2_:Array = null;
         var _loc3_:String = null;
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         var _loc1_:SimpleWindow = this.guiManager.getWindow(SimpleWindow.WINDOW_CLASS_ACHIEVMENT);
         if(_loc1_ != null)
         {
            _loc1_.removeEventListener(SimpleWindow.ON_RESIZE,this.onResizeAchievementWindow);
            _loc1_.removeEventListener(SimpleWindow.ON_RESIZED,this.guiManager.handleWindowResized);
            _loc1_.removeEventListener(SimpleWindow.ON_MAXIMIZE_CLICKED,this.handleMaximizeClicked);
            _loc1_.removeEventListener(SimpleWindow.ON_MAXIMIZED,this.handleMaximized);
            _loc2_ = new Array();
            for(_loc3_ in this.achievements)
            {
               _loc2_.push(_loc3_);
            }
            _loc4_ = 0;
            while(_loc4_ < _loc2_.length)
            {
               _loc5_ = parseInt(_loc2_[_loc4_]);
               this.removeAchievement(_loc5_);
               _loc4_++;
            }
            this.guiManager.closeWindow(_loc1_);
            this.simpleContainer = null;
            this.scrollPane = null;
         }
      }
      
      private function createAchievementWindow() : SimpleWindow
      {
         var _loc1_:SimpleWindow = this.guiManager.createWindow(SimpleWindow.WINDOW_CLASS_ACHIEVMENT);
         _loc1_.maxWindowHeight = 460;
         _loc1_.minWindowHeight = 150;
         var _loc2_:Rectangle = new Rectangle(460,0,0,500);
         _loc1_.setResizementBounds(_loc2_);
         _loc1_.addEventListener(SimpleWindow.ON_RESIZE,this.onResizeAchievementWindow);
         _loc1_.addEventListener(SimpleWindow.ON_RESIZED,this.guiManager.handleWindowResized);
         _loc1_.addEventListener(SimpleWindow.ON_MAXIMIZE_CLICKED,this.handleMaximizeClicked);
         _loc1_.addEventListener(SimpleWindow.ON_MAXIMIZED,this.handleMaximized);
         this.simpleContainer = new SimpleContainer(this.guiManager,SimpleContainer.CLASS_DEFAULT);
         this.scrollPanePaddingY = 16;
         var _loc3_:Bitmap = ResourceManager.getBitmap("achievement","info_background.png");
         _loc3_.x = 15;
         _loc3_.y = 38;
         this.scrollPanePaddingY += _loc3_.y;
         _loc1_.getRootContainer().addChild(_loc3_);
         var _loc4_:TextField = new TextField();
         var _loc5_:TextFormat = new TextFormat(Styles.plainBigFmt.font,Styles.plainStdFontHeight,16777215);
         _loc5_.align = TextFormatAlign.LEFT;
         _loc4_.defaultTextFormat = _loc5_;
         _loc4_.embedFonts = Styles.plainStdEmbed;
         _loc4_.wordWrap = true;
         _loc4_.multiline = true;
         _loc4_.antiAliasType = AntiAliasType.ADVANCED;
         _loc4_.autoSize = TextFieldAutoSize.LEFT;
         _loc4_.selectable = false;
         _loc4_.width = _loc3_.width - 32 - 4;
         _loc4_.text = BPLocale.getText("achievement_header");
         _loc3_.height = _loc4_.height + 10;
         this.scrollPanePaddingY += _loc3_.height;
         _loc4_.x = _loc3_.x + 4;
         _loc4_.y = 38 + 4;
         _loc1_.getRootContainer().addChild(_loc4_);
         this.scrollPane = new ScrollPane();
         this.scrollPane.source = this.simpleContainer;
         this.scrollPane.move(_loc3_.x,_loc3_.y + _loc3_.height);
         _loc1_.getRootContainer().addChild(this.scrollPane);
         return _loc1_;
      }
      
      private function handleMaximizeClicked(param1:Event) : void
      {
         var _loc2_:SimpleWindow = this.guiManager.getWindow(SimpleWindow.WINDOW_CLASS_ACHIEVMENT);
         this.guiManager.stopFlashWindowIcon(_loc2_.classID);
      }
      
      private function handleMaximized(param1:Event) : void
      {
         TweenMax.delayedCall(0.5,this.checkUpdateBuffer);
      }
      
      private function checkUpdateBuffer() : void
      {
         var _loc1_:UpdateBuffer = null;
         if(this.updateBufferList.length > 0)
         {
            _loc1_ = this.updateBufferList.shift();
            this.updateAchievement(_loc1_.achievementID,_loc1_.achievementDone,_loc1_.bargainState);
         }
      }
      
      private function handleBtnClick(param1:MouseEvent) : void
      {
      }
      
      private function onResizeAchievementWindow(param1:Event) : void
      {
         var _loc2_:SimpleWindow = this.guiManager.getWindow(SimpleWindow.WINDOW_CLASS_ACHIEVMENT);
         if(_loc2_ != null && this.scrollPane != null)
         {
            this.scrollPane.setSize(450,_loc2_.getWindow().height - this.scrollPanePaddingY);
            this.scrollPane.refreshPane();
         }
      }
      
      public function setAchievement(param1:int, param2:Boolean, param3:int) : void
      {
         var _loc4_:AchievementElement = this.getAchievement(param1);
         if(_loc4_ == null)
         {
            this.addAchievement(param1,-1,param2,param3);
         }
         else
         {
            this.updateAchievement(param1,param2,param3);
         }
      }
      
      public function addAchievement(param1:int, param2:int, param3:Boolean, param4:int) : AchievementElement
      {
         var _loc5_:SimpleWindow = this.guiManager.getWindow(SimpleWindow.WINDOW_CLASS_ACHIEVMENT);
         if(_loc5_ == null)
         {
            _loc5_ = this.createAchievementWindow();
            this.loadAchievementPrices();
         }
         var _loc6_:AchievementElement = new AchievementElement(this.guiManager.getMain().getConnectionManager(),param1,param2,param3,param4);
         var _loc7_:AchievementPattern = PatternManager.achievementPatterns[param1];
         var _loc8_:String = _loc7_.languageKey;
         _loc6_.setAchievementText("achievement_" + _loc8_ + "_header","achievement_" + _loc8_ + "_directive");
         _loc6_.setRewardText("achievement_" + _loc8_ + "_reward");
         _loc6_.setTooltip("achievement_" + _loc8_ + "_tooltip");
         this.simpleContainer.addElement(_loc6_,0,0);
         this.achievements.push(_loc6_);
         this.scrollPane.setSize(450,200);
         this.scrollPane.invalidate();
         this.scrollPane.refreshPane();
         _loc5_.refreshMask();
         return _loc6_;
      }
      
      public function removeAchievement(param1:int) : void
      {
         var _loc2_:AchievementElement = null;
         var _loc3_:int = 0;
         while(_loc3_ < this.achievements.length)
         {
            _loc2_ = this.achievements[_loc3_];
            if(_loc2_.achievementID == param1)
            {
               this.achievements.splice(_loc3_,1);
               _loc2_.cleanup();
               this.simpleContainer.removeElement(_loc2_);
               break;
            }
            _loc3_++;
         }
      }
      
      private function getAchievement(param1:int) : AchievementElement
      {
         var _loc3_:AchievementElement = null;
         var _loc2_:int = 0;
         while(_loc2_ < this.achievements.length)
         {
            _loc3_ = this.achievements[_loc2_];
            if(_loc3_.achievementID == param1)
            {
               return _loc3_;
            }
            _loc2_++;
         }
         return null;
      }
      
      public function updateAchievement(param1:int, param2:Boolean, param3:int) : void
      {
         var _loc6_:int = 0;
         var _loc7_:AchievementElement = null;
         var _loc8_:int = 0;
         var _loc9_:Boolean = false;
         var _loc10_:AchievementElement = null;
         var _loc11_:AchievementElement = null;
         var _loc12_:int = 0;
         var _loc13_:AchievementElement = null;
         var _loc14_:int = 0;
         var _loc4_:SimpleWindow = this.guiManager.getWindow(SimpleWindow.WINDOW_CLASS_ACHIEVMENT);
         var _loc5_:AchievementElement = this.getAchievement(param1);
         if(_loc5_.bargainState == param3 && _loc5_.achievementDone == param2)
         {
            return;
         }
         if(_loc4_ != null)
         {
            if(!_loc4_.isMaximized())
            {
               this.updateBufferList.push(new UpdateBuffer(param1,param2,param3));
               AudioManager.playSoundEffect(51);
               this.guiManager.flashWindowIcon(_loc4_.classID,-1,false);
               return;
            }
            _loc6_ = 0;
            while(_loc6_ < this.achievements.length)
            {
               _loc11_ = this.achievements[_loc6_];
               _loc11_.order = -1;
               _loc6_++;
            }
            _loc7_ = this.achievements[0];
            if(_loc7_.achievementDone)
            {
               _loc7_.order = AchievementPattern.MAX_ID - 1;
            }
            _loc8_ = _loc5_.questID;
            _loc9_ = _loc5_.achievementDone;
            this.removeAchievement(param1);
            _loc10_ = this.addAchievement(param1,_loc8_,_loc9_,param3);
            _loc10_.y = _loc5_.y;
            if(!_loc9_ && param2)
            {
               _loc10_.activate();
               _loc10_.order = 0;
               _loc12_ = 1;
               _loc14_ = 0;
               while(_loc14_ < this.achievements.length)
               {
                  _loc13_ = this.achievements[_loc14_];
                  if(_loc13_.order == -1)
                  {
                     _loc13_.order = _loc12_;
                     _loc12_++;
                  }
                  _loc14_++;
               }
               this.resortAchievements();
               TweenLite.to(this.scrollPane,1,{"verticalScrollPosition":0});
            }
            else if(_loc9_ && !param2)
            {
               _loc10_.deactivate();
            }
            if(param2)
            {
               AudioManager.playSoundEffect(51);
               if(!_loc4_.isMaximized())
               {
                  this.guiManager.flashWindowIcon(_loc4_.classID,-1,false);
               }
            }
         }
      }
      
      private function resortAchievements() : void
      {
         var _loc3_:AchievementElement = null;
         this.achievements.sortOn("order",Array.NUMERIC);
         var _loc1_:int = 0;
         var _loc2_:int = 0;
         while(_loc2_ < this.achievements.length)
         {
            _loc3_ = this.achievements[_loc2_];
            _loc3_.y = _loc1_;
            _loc1_ += 72;
            _loc2_++;
         }
         this.invalidateScrollPane();
      }
      
      private function invalidateScrollPane() : void
      {
         this.scrollPane.invalidate();
         this.scrollPane.refreshPane();
      }
      
      public function loadAchievementPrices() : void
      {
         var _loc1_:URLRequest = new URLRequest(Settings.dynamicHost + "flashinput/dynamicPaymentItems.php");
         var _loc2_:URLLoader = new URLLoader();
         _loc2_.addEventListener(Event.COMPLETE,this.handleAchievementPricesXMLLoaded);
         _loc2_.addEventListener(IOErrorEvent.IO_ERROR,this.handleXMLLoadingError);
         _loc2_.load(_loc1_);
      }
      
      private function handleAchievementPricesXMLLoaded(param1:Event) : void
      {
         var xml:XML = null;
         var item:XML = null;
         var achievementID:int = 0;
         var priceValue:Number = NaN;
         var priceCurrency:String = null;
         var achievementPattern:AchievementPattern = null;
         var achievement:AchievementElement = null;
         var event:Event = param1;
         try
         {
            xml = XML(event.currentTarget.data);
         }
         catch(e:Error)
         {
         }
         for each(item in xml.achievements.item)
         {
            achievementID = int(item.@id);
            priceValue = Number(item.@price);
            priceCurrency = item.@currency;
            achievementPattern = PatternManager.achievementPatterns[achievementID];
            if(achievementPattern != null)
            {
               achievementPattern.priceValue = priceValue;
               achievementPattern.priceCurrency = priceCurrency;
            }
            achievement = this.getAchievement(achievementID);
            achievement.updatePriceField();
         }
      }
      
      private function handleXMLLoadingError(param1:IOErrorEvent) : void
      {
      }
   }
}



/************************************/

From now on this is the new file that controls the achievements in the game. BUT the compilation os AS did not retain the variables names, and i since the achivement system was descontinued back in the old version, i can't find the original source code to compare and rename the variables, so i will not be able to provide a readable version of the code.

package net.bigpoint.darkorbit.net
{
   import _SafePkg_137.Effect;
   import _SafePkg_137._SafeCls_464;
   import _SafePkg_100._SafeCls_393;
   import _SafePkg_54._SafeCls_88;
   import _SafePkg_497._SafeCls_496;
   import _SafePkg_175._SafeCls_499;
   import _SafePkg_392._SafeCls_391;
   import _SafePkg_182._SafeCls_181;
   import _SafePkg_84._SafeCls_83;
   import _SafePkg_281._SafeCls_500;
   import _SafePkg_281._SafeCls_502;
   import _SafePkg_281._SafeCls_503;
   import _SafePkg_281._SafeCls_486;
   import _SafePkg_281._SafeCls_504;
   import _SafePkg_281._SafeCls_478;
   import _SafePkg_32.ResourceManager;
   import _SafePkg_32._SafeCls_67;
   import _SafePkg_114._SafeCls_113;
   import _SafePkg_131._SafeCls_130;
   import _SafePkg_118._SafeCls_119;
   import _SafePkg_126._SafeCls_125;
   import _SafePkg_267._SafeCls_266;
   import _SafePkg_267._SafeCls_390;
   import com.bigpoint.utils._SafeCls_170;
   import com.greensock.TweenLite;
   import flash.display.Bitmap;
   import flash.display.BitmapData;
   import flash.display.DisplayObject;
   import flash.display.MovieClip;
   import flash.events.MouseEvent;
   import flash.events.TimerEvent;
   import flash.filters.GlowFilter;
   import flash.geom.Rectangle;
   import flash.text.AntiAliasType;
   import flash.text.TextField;
   import flash.text.TextFieldAutoSize;
   import flash.utils.Dictionary;
   import flash.utils.Timer;
   import mx.utils.StringUtil;
   import net.bigpoint.as3toolbox.bplocale._SafeCls_122;
   import net.bigpoint.darkorbit._SafeCls_31;
   import net.bigpoint.darkorbit._SafeCls_21;
   import net.bigpoint.darkorbit._SafeCls_112;
   import net.bigpoint.darkorbit._SafeCls_176;
   import net.bigpoint.darkorbit.gui._SafeCls_498;
   import net.bigpoint.darkorbit.gui._SafeCls_79;
   import net.bigpoint.darkorbit.gui._SafeCls_271;
   import net.bigpoint.darkorbit.map.model.ship._SafeCls_120;
   import net.bigpoint.darkorbit.map.model.traits._SafeCls_425;
   import net.bigpoint.darkorbit.mvc.common.AssetNotifications;
   import net.bigpoint.darkorbit.mvc.common.model.assets.AssetsProxy;
   import net.bigpoint.darkorbit.mvc.gui.GuiConstants;
   import net.bigpoint.darkorbit.settings.Settings;
   
   public class _SafeCls_155 extends _SafeCls_135
   {
      
      private static var instance:_SafeCls_155;
      
      private static const _SafeStr_3496:GlowFilter = new GlowFilter(13809664,1,6,6,2,1,true);
      
      private var _SafeStr_17328:Dictionary;
      
      private var _SafeStr_3614:Dictionary;
      
      private var _SafeStr_5151:_SafeCls_501;
      
      private var _SafeStr_8618:Dictionary;
      
      private var main:_SafeCls_21;
      
      private var _SafeStr_16179:Timer = new Timer(1000 * 6,1);
      
      private var _SafeStr_14561:Boolean = false;
      
      private var _SafeStr_16964:Array = [];
      
      private var achievements:Dictionary = new Dictionary();
      
      private var _SafeStr_4212:_SafeCls_83;
      
      public function _SafeCls_155(param1:Function)
      {
         super();
         if(param1 !== _SafeStr_4070)
         {
            throw new Error("SetAttributeAssembly is a Singleton and can only be accessed through SetAttributeAssembly.getInstance()");
         }
         this.main = _SafeStr_4896;
         this._SafeStr_4897();
         this._SafeStr_17061();
         this._SafeStr_5151 = _SafeCls_501.getInstance();
         this._SafeStr_4212 = _SafeCls_83.getInstance();
      }
      
      public static function getInstance() : _SafeCls_155
      {
         if(instance == null)
         {
            instance = new _SafeCls_155(_SafeStr_4070);
         }
         return instance;
      }
      
      private static function _SafeStr_4070() : void
      {
      }
      
      private function _SafeStr_17061() : void
      {
         this._SafeStr_17328 = new Dictionary();
         this._SafeStr_17328[3] = _SafeCls_498._SafeStr_18117;
         this._SafeStr_17328[4] = _SafeCls_498._SafeStr_11842;
         this._SafeStr_17328[5] = _SafeCls_498._SafeStr_12135;
         this._SafeStr_17328[6] = _SafeCls_498._SafeStr_15969;
         this._SafeStr_17328[7] = _SafeCls_498._SafeStr_10562;
         this._SafeStr_17328[8] = _SafeCls_498._SafeStr_14813;
         this._SafeStr_17328[10] = _SafeCls_498._SafeStr_16139;
         this._SafeStr_17328[11] = _SafeCls_498._SafeStr_8099;
         this._SafeStr_17328[12] = _SafeCls_498._SafeStr_7070;
         this._SafeStr_17328[13] = _SafeCls_498._SafeStr_5083;
         this._SafeStr_17328[14] = _SafeCls_498._SafeStr_7524;
         this._SafeStr_17328[15] = _SafeCls_498._SafeStr_4307;
         this._SafeStr_17328[16] = _SafeCls_498._SafeStr_14985;
         this._SafeStr_17328[17] = _SafeCls_498._SafeStr_9112;
         this._SafeStr_17328[18] = _SafeCls_498._SafeStr_7405;
         this._SafeStr_17328[19] = _SafeCls_498._SafeStr_14118;
      }
      
      public function _SafeStr_14980(param1:Array) : void
      {
         var _loc2_:String = param1[2];
         if(this._SafeStr_3614[_loc2_] != null)
         {
            this._SafeStr_3614[_loc2_](param1);
         }
      }
      
      private function _SafeStr_4897() : void
      {
         this._SafeStr_3614 = new Dictionary();
         this._SafeStr_3614[_SafeCls_101._SafeStr_12610] = this._SafeStr_7210;
         this._SafeStr_3614[_SafeCls_101._SafeStr_7432] = this._SafeStr_8196;
         this._SafeStr_3614[_SafeCls_101._SafeStr_12149] = this._SafeStr_7158;
         this._SafeStr_3614[_SafeCls_101._SafeStr_12327] = this._SafeStr_13975;
         this._SafeStr_3614[_SafeCls_101._SafeStr_4998] = this._SafeStr_9349;
         this._SafeStr_3614[_SafeCls_101._SafeStr_7650] = this._SafeStr_9576;
         this._SafeStr_3614[_SafeCls_101._SafeStr_7561] = this._SafeStr_4104;
         this._SafeStr_3614[_SafeCls_101._SafeStr_17499] = this._SafeStr_7938;
         this._SafeStr_3614[_SafeCls_101._SafeStr_14254] = this._SafeStr_12479;
         this._SafeStr_3614[_SafeCls_101._SafeStr_15641] = this._SafeStr_16687;
         this._SafeStr_3614[_SafeCls_101._SafeStr_17768] = this._SafeStr_7822;
         this._SafeStr_3614[_SafeCls_101._SafeStr_12063] = this._SafeStr_10283;
         this._SafeStr_3614[_SafeCls_101._SafeStr_16305] = this._SafeStr_8703;
         this._SafeStr_3614[_SafeCls_101._SafeStr_6792] = this._SafeStr_9627;
         this._SafeStr_3614[_SafeCls_101._SafeStr_8834] = this._SafeStr_13075;
         this._SafeStr_3614[_SafeCls_101._SafeStr_14050] = this._SafeStr_13293;
         this._SafeStr_3614[_SafeCls_101._SafeStr_8573] = this._SafeStr_11020;
         this._SafeStr_3614[_SafeCls_101._SafeStr_13369] = this._SafeStr_13182;
         this._SafeStr_3614[_SafeCls_101._SafeStr_13663] = this._SafeStr_4540;
         this._SafeStr_3614[_SafeCls_101._SafeStr_9910] = this._SafeStr_9260;
         this._SafeStr_3614[_SafeCls_101._SafeStr_12474] = this._SafeStr_7801;
         this._SafeStr_3614[_SafeCls_101._SafeStr_11268] = this._SafeStr_6944;
         this._SafeStr_3614[_SafeCls_101._SafeStr_11566] = this._SafeStr_6944;
         this._SafeStr_3614[_SafeCls_101._SafeStr_4450] = this._SafeStr_6944;
         this._SafeStr_3614[_SafeCls_101._SafeStr_15961] = this._SafeStr_6944;
         this._SafeStr_3614[_SafeCls_101._SafeStr_15571] = this._SafeStr_15304;
         this._SafeStr_3614[_SafeCls_101._SafeStr_13080] = this._SafeStr_8375;
         this._SafeStr_3614[_SafeCls_101._SafeStr_9917] = this._SafeStr_3931;
         this._SafeStr_3614[_SafeCls_101._SafeStr_10047] = this._SafeStr_14001;
         this._SafeStr_3614[_SafeCls_101._SafeStr_15219] = this._SafeStr_7126;
         this._SafeStr_3614[_SafeCls_101._SafeStr_17955] = this._SafeStr_5140;
      }
      
      private function _SafeStr_7126(param1:Array) : void
      {
         var _loc5_:_SafeCls_120 = null;
         var _loc6_:_SafeCls_486 = null;
         var _loc9_:int = 0;
         var _loc2_:int = int(param1[3]);
         var _loc3_:int = int(param1[4]);
         var _loc4_:int = -1;
         if(param1.length > 5)
         {
            _loc4_ = int(param1[5]);
         }
         if(_loc2_ == 1)
         {
            _loc5_ = this.main._SafeStr_6579.map._SafeStr_6052()._SafeStr_11080(_loc3_);
         }
         else
         {
            _loc5_ = this.main._SafeStr_6579.map._SafeStr_6052()._SafeStr_8961(_loc3_);
         }
         if(!_loc5_)
         {
            return;
         }
         var _loc7_:int = 1;
         var _loc8_:Boolean = false;
         if(_loc4_ > -1)
         {
            _loc7_ = _loc4_;
            _loc8_ = true;
         }
         _SafeCls_113.playSoundEffect(3,false,false,_loc5_.x,_loc5_.y);
         map.effects._SafeStr_14987(new _SafeCls_486(_loc5_,_loc7_,_loc8_));
      }
      
      private function _SafeStr_5140(param1:Array) : void
      {
         var _loc4_:_SafeCls_120 = null;
         var _loc2_:int = int(param1[3]);
         var _loc3_:int = int(param1[4]);
         if(_loc2_ == 1)
         {
            _loc4_ = this.main._SafeStr_6579.map._SafeStr_6052()._SafeStr_11080(_loc3_);
         }
         else
         {
            _loc4_ = this.main._SafeStr_6579.map._SafeStr_6052()._SafeStr_8961(_loc3_);
         }
         map.effects._SafeStr_10659(_loc4_.id,_SafeCls_464._SafeStr_13936);
         var _loc5_:String = _SafeCls_122.getItem("msg_loot_error_generic");
         this.main._SafeStr_13608().writeToLog(_loc5_);
      }
      
      private function _SafeStr_9260(param1:Array) : void
      {
         var _loc2_:String = String(param1[3]);
         var _loc3_:int = 0;
         var _loc4_:int = 0;
         switch(_loc2_)
         {
            case _SafeCls_101._SafeStr_3379:
               this._SafeStr_5283(param1);
               break;
            case _SafeCls_101._SafeStr_13535:
               this._SafeStr_4820(param1);
               break;
            case _SafeCls_101._SafeStr_11403:
               _loc3_ = int(param1[4]);
               _loc4_ = int(param1[5]);
               this.main._SafeStr_13608()._SafeStr_17989(_loc3_,_loc4_);
               map.effects._SafeStr_3927(new _SafeCls_500(this.hero.x,this.hero.y));
               if(_loc3_ != -1)
               {
                  this.main._SafeStr_6579._SafeStr_7347._SafeStr_16902();
                  break;
               }
               this.main._SafeStr_6579._SafeStr_7347._SafeStr_3507();
         }
      }
      
      private function _SafeStr_4820(param1:Array) : void
      {
         var _loc2_:int = 3;
         var _loc3_:int = int(param1[++_loc2_]);
         var _loc4_:int = int(param1[++_loc2_]);
         var _loc5_:int = int(param1[++_loc2_]);
         this.main._SafeStr_13608()._SafeStr_17978(_loc3_,_loc4_,_loc5_);
      }
      
      private function _SafeStr_5283(param1:Array) : void
      {
         var _loc2_:String = "";
         var _loc3_:String = "-1";
         var _loc4_:String = "-2";
         var _loc5_:Array = [];
         var _loc6_:Array = [];
         var _loc7_:Array = [];
         var _loc8_:Array = param1.slice(4,param1.length);
         if(param1.length < 5)
         {
            this.main._SafeStr_13608()._SafeStr_4045();
            return;
         }
         var _loc9_:int = 0;
         while(_loc9_ < _loc8_.length)
         {
            _loc2_ = String(_loc8_[_loc9_]);
            if(_loc2_.split(";").length < 2)
            {
               if(_loc2_ == _loc3_)
               {
                  _loc5_ = this._SafeStr_6411(_loc8_,_loc9_);
               }
               else if(_loc2_ == _loc4_)
               {
                  _loc6_ = this._SafeStr_6411(_loc8_,_loc9_);
               }
               else
               {
                  _loc7_.push([_loc2_,this._SafeStr_6411(_loc8_,_loc9_)]);
               }
            }
            _loc9_++;
         }
         this.main._SafeStr_13608()._SafeStr_11589(_loc7_,_loc5_,_loc6_);
      }
      
      private function _SafeStr_6411(param1:Array, param2:int) : Array
      {
         var _loc3_:Array = String(param1[param2 + 1]).split(";");
         _loc3_.pop();
         return _loc3_;
      }
      
      private function _SafeStr_11020(param1:Array) : void
      {
         var _loc2_:Boolean = Boolean(int(param1[3]));
         this.main._SafeStr_17416()._SafeStr_11476 = _loc2_;
      }
      
      private function _SafeStr_13182(param1:Array) : void
      {
      }
      
      private function _SafeStr_4540(param1:Array) : void
      {
         _SafeCls_21._SafeStr_5073 = param1[3];
         this.main._SafeStr_13608().writeToLog("Server Version: " + _SafeCls_21._SafeStr_5073);
      }
      
      private function _SafeStr_13293(param1:Array) : void
      {
      }
      
      private function _SafeStr_13075(param1:Array) : void
      {
         var _loc2_:String = param1[3];
         var _loc3_:String = StringUtil.trim(param1[4]);
         _SafeCls_112._SafeStr_6708 = new _SafeCls_393(_loc2_,_loc3_);
      }
      
      public function _SafeStr_9627(param1:Boolean, param2:int, param3:int) : void
      {
         if(param1)
         {
            map.effects._SafeStr_16113(new _SafeCls_503(map.hero,Effect._SafeStr_14029,param2,param3));
         }
         else
         {
            map.effects._SafeStr_10659(_SafeCls_112.userID,_SafeCls_464._SafeStr_15226);
         }
      }
      
      private function _SafeStr_8375(param1:Array) : void
      {
         var _loc12_:_SafeCls_390 = null;
         var _loc2_:int = int(param1[3]);
         var _loc3_:int = int(param1[4]);
         var _loc4_:Number = Number(param1[5]);
         var _loc5_:Number = Number(param1[6]);
         var _loc6_:int = int(param1[7]);
         var _loc7_:int = int(param1[8]);
         var _loc8_:int = int(param1[9]);
         var _loc9_:int = 20;
         var _loc10_:Number = _loc9_ / 100 * _loc7_;
         var _loc11_:_SafeCls_181 = this.main._SafeStr_13608()._SafeStr_4361(GuiConstants.SCOREEVENT_WINDOW);
         if(!_loc11_)
         {
            this.main._SafeStr_13608()._SafeStr_14251();
         }
         if(_SafeCls_112._SafeStr_5910 == null)
         {
            _loc12_ = new _SafeCls_390();
            _SafeCls_112._SafeStr_5910 = _loc12_;
         }
         _SafeCls_112._SafeStr_5910._SafeStr_9728 = _loc2_;
         _SafeCls_112._SafeStr_5910._SafeStr_4333 = _loc3_;
         _SafeCls_112._SafeStr_5910._SafeStr_9571 = _loc4_;
         _SafeCls_112._SafeStr_5910._SafeStr_4076 = _loc5_;
         _SafeCls_112._SafeStr_5910._SafeStr_12339 = _loc6_;
         _SafeCls_112._SafeStr_5910._SafeStr_17597 = _loc7_;
         _SafeCls_112._SafeStr_5910._SafeStr_14844 = _loc10_;
         _SafeCls_112._SafeStr_5910.points = _loc8_;
         this.main._SafeStr_13608()._SafeStr_5233();
      }
      
      private function _SafeStr_3931(param1:Array) : void
      {
         var _loc2_:_SafeCls_181 = this.main._SafeStr_13608()._SafeStr_4361(GuiConstants.SCOREEVENT_WINDOW);
         if(_loc2_)
         {
            this.main._SafeStr_13608()._SafeStr_9340(GuiConstants.SCOREEVENT_WINDOW);
         }
      }
      
      private function _SafeStr_15304(param1:Array) : void
      {
         var _loc6_:_SafeCls_390 = null;
         var _loc2_:int = int(param1[3]);
         var _loc3_:int = int(param1[4]);
         var _loc4_:int = int(param1[5]);
         var _loc5_:_SafeCls_181 = this.main._SafeStr_13608()._SafeStr_4361(GuiConstants.HIGH_SCORE_GATE_WINDOW);
         if(!_loc5_)
         {
            this.main._SafeStr_13608()._SafeStr_16513();
         }
         if(_SafeCls_112._SafeStr_12260 == null)
         {
            _loc6_ = new _SafeCls_390();
            _SafeCls_112._SafeStr_12260 = _loc6_;
         }
         _SafeCls_112._SafeStr_12260._SafeStr_3711 = _loc2_;
         _SafeCls_112._SafeStr_12260._SafeStr_16912 = _loc3_;
         _SafeCls_112._SafeStr_12260.points = _loc4_;
         this.main._SafeStr_13608()._SafeStr_11611();
      }
      
      private function _SafeStr_14001(param1:Array) : void
      {
         var _loc5_:_SafeCls_499 = null;
         var _loc2_:String = String(param1[3]);
         var _loc3_:int = int(param1[4]);
         var _loc4_:Vector.<_SafeCls_119> = _SafeCls_67.getInstance().map._SafeStr_7398;
         for each(_loc5_ in _loc4_)
         {
            if(Boolean(_loc5_) && _loc5_._SafeStr_16887 == _SafeCls_499._SafeStr_7802)
            {
               _loc5_.text.left = _loc2_;
               _loc5_.text.right = _loc3_.toString();
            }
         }
      }
      
      private function _SafeStr_8703(param1:Array) : void
      {
         var _loc6_:String = null;
         var _loc7_:String = null;
         var _loc8_:int = 0;
         var _loc9_:Array = null;
         var _loc10_:int = 0;
         var _loc11_:Array = null;
         var _loc12_:String = null;
         var _loc13_:int = 0;
         var _loc14_:String = null;
         if(_SafeCls_112._SafeStr_3564 == null)
         {
            this._SafeStr_9920(_SafeCls_391._SafeStr_15130);
         }
         var _loc2_:int = int(param1[3]);
         var _loc3_:String = param1[4];
         var _loc4_:int = int(param1[5]);
         if(_SafeCls_112._SafeStr_3564 == null)
         {
            _SafeCls_112._SafeStr_3564 = new _SafeCls_391();
         }
         if(_SafeCls_112._SafeStr_3564._SafeStr_13794 == null)
         {
            _SafeCls_112._SafeStr_3564._SafeStr_13794 = [];
         }
         var _loc5_:_SafeCls_266 = _SafeCls_112._SafeStr_3564._SafeStr_13794[_loc2_] as _SafeCls_266;
         if(_loc5_ == null)
         {
            this.main._SafeStr_17416().sendCommand(_SafeCls_160._SafeStr_9427,[_SafeCls_101._SafeStr_16305,_loc2_]);
            _loc5_ = new _SafeCls_266();
            _SafeCls_112._SafeStr_3564._SafeStr_13794[_loc2_] = _loc5_;
         }
         switch(_loc3_)
         {
            case _SafeCls_101._SafeStr_10090:
               if(_loc5_._SafeStr_14637 != 0)
               {
                  _loc5_._SafeStr_10010 = _loc4_ - _loc5_._SafeStr_14637;
               }
               _loc5_._SafeStr_14637 = _loc4_;
               if(_loc5_._SafeStr_10010 > 0 && _loc5_.targetList != null)
               {
                  if(_loc5_._SafeStr_10010 == 1)
                  {
                     this.main._SafeStr_13608().writeToLog(_SafeCls_122.getItem("log_msg_ranked_hunt_point_s"));
                  }
                  else
                  {
                     this.main._SafeStr_13608().writeToLog(_SafeCls_122.getItem("log_msg_ranked_hunt_point_p").replace(/%AMOUNT%/,_SafeCls_170._SafeStr_3518(_loc5_._SafeStr_10010)));
                  }
               }
               _loc5_._SafeStr_7418 = false;
               break;
            case _SafeCls_101._SafeStr_4680:
               _loc5_._SafeStr_10010 = 0;
               _loc5_._SafeStr_7418 = false;
               _loc5_._SafeStr_6541 = _loc4_;
               _loc5_._SafeStr_7418 = true;
               break;
            case _SafeCls_101._SafeStr_12557:
               _loc6_ = param1[5];
               _loc7_ = param1[6];
               if(param1[7] == undefined)
               {
                  _loc8_ = _SafeCls_391._SafeStr_15130;
               }
               else
               {
                  _loc8_ = int(param1[7]);
               }
               switch(_loc6_)
               {
                  case _SafeCls_101._SafeStr_15315:
                     _loc5_ = _SafeCls_112._SafeStr_3564._SafeStr_13794[_loc2_] as _SafeCls_266;
                     if(_loc5_ != null)
                     {
                        _loc5_._SafeStr_3506 = _loc8_;
                        this._SafeStr_9920(_loc8_);
                        _loc9_ = _loc7_.split(",");
                        _loc5_.targetList = Vector.<int>(_loc9_);
                        _loc10_ = int(_loc5_.targetList.length);
                        _loc11_ = _SafeCls_176.instance._SafeStr_7394;
                        _loc12_ = "";
                        _loc13_ = 0;
                        while(_loc13_ < _loc10_)
                        {
                           _loc14_ = _loc11_[_loc5_.targetList[_loc13_]];
                           if(_loc14_ != null)
                           {
                              _loc12_ += ", " + _loc14_;
                           }
                           _loc13_++;
                        }
                        _loc12_ = _loc12_.substring(2);
                        if(_loc5_.targetList.length > 1)
                        {
                           _loc5_._SafeStr_9639 = _SafeCls_122.getItem("q2_condition_KILL_NPC").replace(/%npc%/,_loc12_);
                           break;
                        }
                        _loc5_._SafeStr_9639 = _SafeCls_122.getItem("q2_condition_KILL_NPCS").replace(/%npcs%/,_loc12_);
                     }
                     break;
                  case _SafeCls_101._SafeStr_14447:
               }
         }
         _SafeCls_112._SafeStr_3564._SafeStr_12038 = _loc2_;
         if(_loc5_.targetList != null)
         {
            this.main._SafeStr_13608()._SafeStr_7711(_loc2_);
         }
      }
      
      private function _SafeStr_9920(param1:int) : void
      {
         switch(param1)
         {
            case _SafeCls_391._SafeStr_6091:
               _SafeCls_122._SafeStr_14533("title_ranked_hunt",_SafeCls_122.getItem("title_pirate_hunt"));
               _SafeCls_122._SafeStr_14533("ttip_ranked_hunt_point",_SafeCls_122.getItem("ttip_bounty_points"));
               _SafeCls_122._SafeStr_14533("ttip_clan_ranked_hunt_point",_SafeCls_122.getItem("ttip_clan_bounty_points"));
               _SafeCls_122._SafeStr_14533("log_msg_ranked_hunt_point_s",_SafeCls_122.getItem("log_msg_bounty_point"));
               _SafeCls_122._SafeStr_14533("log_msg_ranked_hunt_point_p",_SafeCls_122.getItem("log_msg_bounty_points"));
               break;
            case _SafeCls_391._SafeStr_15130:
            default:
               _SafeCls_122._SafeStr_14533("title_ranked_hunt",_SafeCls_122.getItem("title_npc_hunt"));
               _SafeCls_122._SafeStr_14533("ttip_ranked_hunt_point",_SafeCls_122.getItem("ttip_npc_hunt_point"));
               _SafeCls_122._SafeStr_14533("ttip_clan_ranked_hunt_point",_SafeCls_122.getItem("ttip_clan_npc_hunt_point"));
               _SafeCls_122._SafeStr_14533("log_msg_ranked_hunt_point_s",_SafeCls_122.getItem("log_msg_npc_hunt_point_s"));
               _SafeCls_122._SafeStr_14533("log_msg_ranked_hunt_point_p",_SafeCls_122.getItem("log_msg_npc_hunt_point_p"));
         }
      }
      
      private function _SafeStr_10283(param1:Array) : void
      {
         var _loc2_:int = int(param1[3]);
         Settings.selectedConfiguration = _loc2_;
         this.main._SafeStr_13608().updateInfoField(GuiConstants.SHIP_WINDOW,_SafeCls_130._SafeStr_14011,_SafeCls_125._SafeStr_13291);
      }
      
      private function _SafeStr_7822(param1:Array) : void
      {
         _SafeCls_112._SafeStr_4405.value = int(param1[3]);
      }
      
      public function _SafeStr_16687(param1:int) : void
      {
         if(this.hero != null)
         {
            this.hero.speed.value = param1;
         }
      }
      
      private function _SafeStr_7938(param1:Array) : void
      {
         var _loc2_:int = int(param1[3]);
         var _loc3_:int = int(param1[4]);
         this.main._SafeStr_13608()._SafeStr_13139(_loc2_,_loc3_);
         var _loc4_:_SafeCls_120 = this.hero;
         if(_loc4_)
         {
            map.effects._SafeStr_14987(new _SafeCls_502(_loc4_));
         }
         _SafeCls_80.call(_SafeCls_80._SafeStr_5070,[{
            "level":_loc2_,
            "event":"levelUp"
         }]);
      }
      
      private function _SafeStr_12479(param1:Array) : void
      {
         if(map != null && this.hero != null)
         {
            map.effects._SafeStr_14987(new _SafeCls_504(map.hero));
            this._SafeStr_7483(param1[3],param1[4],param1[5]);
            this._SafeStr_6039();
         }
      }
      
      private function _SafeStr_6039() : void
      {
         _SafeCls_113.playSoundEffect(_SafeCls_176._SafeStr_8259,false,false,this.hero.x,this.hero.y);
      }
      
      private function _SafeStr_7483(param1:String, param2:String = null, param3:String = null) : void
      {
         var _loc4_:Object = new Object();
         if(!param1)
         {
            return;
         }
         if(param2 == null)
         {
            _loc4_["count"] = "";
         }
         else
         {
            _loc4_["count"] = param2;
         }
         if(param3 == null)
         {
            _loc4_["time"] = "";
         }
         else
         {
            _loc4_["time"] = param3;
         }
         this.achievements[param1] = _loc4_;
         this._SafeStr_4212._SafeStr_8840(AssetNotifications.LOAD_ASSET,[param1 + "_30x30",this._SafeStr_13763,null,AssetsProxy.ACHIEVEMENTS]);
      }
      
      private function _SafeStr_13763(param1:_SafeCls_88) : void
      {
         var _loc2_:_SafeCls_88 = param1;
         var _loc3_:BitmapData = ResourceManager.getBitmapData("ui","achievementBG");
         var _loc4_:String = _loc2_._SafeStr_7052.id;
         var _loc5_:RegExp = /_30x30/;
         var _loc6_:String = _loc4_.replace(_loc5_,"");
         var _loc7_:Object = this.achievements[_loc6_];
         var _loc8_:String = _SafeCls_122._SafeStr_4981("achievements",_loc6_ + "_name");
         var _loc9_:TextField = new TextField();
         _loc9_.selectable = false;
         _loc9_.autoSize = TextFieldAutoSize.LEFT;
         _loc9_.defaultTextFormat = _SafeCls_31._SafeStr_10377;
         _loc9_.antiAliasType = AntiAliasType.ADVANCED;
         _loc9_.embedFonts = _SafeCls_31._SafeStr_13733;
         _loc9_.text = _loc8_;
         _loc9_.x = 42;
         _loc9_.y = 9;
         var _loc10_:TextField = new TextField();
         _loc10_.selectable = false;
         var _loc11_:String = _SafeCls_122._SafeStr_4981("achievements",_loc6_ + "_description");
         _loc10_.autoSize = TextFieldAutoSize.LEFT;
         var _loc12_:RegExp = /%COUNT%/;
         _loc10_.defaultTextFormat = _SafeCls_31._SafeStr_15100;
         _loc10_.embedFonts = _SafeCls_31._SafeStr_13733;
         _loc10_.antiAliasType = AntiAliasType.ADVANCED;
         var _loc13_:String = _loc11_.replace(_loc12_,_loc7_["count"]);
         _loc10_.text = _loc13_.replace("%TIME%",_loc7_["time"]);
         _loc10_.x = 42;
         _loc10_.y = 24;
         var _loc14_:String = _loc10_.text.replace("<i>","");
         var _loc15_:String = _loc14_.replace("</i>","");
         _loc10_.text = _loc15_;
         var _loc16_:Bitmap = _loc2_.getBitmap();
         var _loc17_:MovieClip = new MovieClip();
         var _loc18_:Number = _loc9_.width >= _loc10_.width ? _loc9_.width : _loc10_.width;
         if(_loc18_ > 240)
         {
            _loc18_ += 50;
         }
         else
         {
            _loc18_ = 280;
         }
         var _loc19_:_SafeCls_496 = new _SafeCls_496(new Rectangle(9,0,1,45),_loc18_,45,_loc3_);
         _loc17_.addChild(_loc19_);
         _loc17_.addChild(_loc16_);
         _loc17_.addChild(_loc9_);
         _loc17_.addChild(_loc10_);
         _loc16_.x = 7;
         _loc16_.y = 9;
         _SafeCls_67.getInstance()._SafeStr_18136.addChild(_loc17_);
         _loc17_.x = 5;
         _loc17_.y = -50;
         if(this._SafeStr_14561)
         {
            this._SafeStr_4001();
         }
         _loc17_.addEventListener(MouseEvent.ROLL_OVER,this._SafeStr_12637);
         _loc17_.addEventListener(MouseEvent.ROLL_OUT,this._SafeStr_18061);
         _loc17_.addEventListener(MouseEvent.CLICK,this._SafeStr_3358);
         _loc17_.mouseChildren = false;
         _loc17_.buttonMode = true;
         TweenLite.to(_loc17_,0.8,{"y":this._SafeStr_16964.length * 45});
         this._SafeStr_16964.push(_loc17_);
         this._SafeStr_16179.reset();
         this._SafeStr_16179.addEventListener(TimerEvent.TIMER_COMPLETE,this._SafeStr_10819);
         this._SafeStr_16179.start();
      }
      
      private function _SafeStr_3358(param1:MouseEvent) : void
      {
         _SafeCls_80.referToURL("internalPilotSheet","seo_title_achievements","achievements",true);
      }
      
      private function _SafeStr_18061(param1:MouseEvent) : void
      {
         var _loc2_:DisplayObject = param1.target as DisplayObject;
         _loc2_.filters = [];
      }
      
      private function _SafeStr_12637(param1:MouseEvent) : void
      {
         var _loc2_:DisplayObject = param1.target as DisplayObject;
         _loc2_.filters = [_SafeStr_3496];
      }
      
      private function _SafeStr_10819(param1:TimerEvent) : void
      {
         var _loc3_:MovieClip = null;
         this._SafeStr_14561 = true;
         var _loc2_:int = 0;
         while(_loc2_ < this._SafeStr_16964.length)
         {
            _loc3_ = this._SafeStr_16964[_loc2_] as MovieClip;
            TweenLite.to(_loc3_,0.8,{
               "y":-50,
               "onComplete":this._SafeStr_4001
            });
            _loc2_++;
         }
         this._SafeStr_16179.removeEventListener(TimerEvent.TIMER_COMPLETE,this._SafeStr_10819);
         this._SafeStr_16179.stop();
      }
      
      private function _SafeStr_4001() : void
      {
         var _loc2_:MovieClip = null;
         var _loc1_:int = 0;
         while(_loc1_ < this._SafeStr_16964.length)
         {
            _loc2_ = this._SafeStr_16964[_loc1_] as MovieClip;
            _loc2_.removeEventListener(MouseEvent.ROLL_OVER,this._SafeStr_12637);
            _loc2_.removeEventListener(MouseEvent.ROLL_OUT,this._SafeStr_18061);
            _loc2_.removeEventListener(MouseEvent.CLICK,this._SafeStr_3358);
            if(_SafeCls_67.getInstance()._SafeStr_18136.contains(_loc2_))
            {
               _SafeCls_67.getInstance()._SafeStr_18136.removeChild(_loc2_);
               TweenLite.killTweensOf(_loc2_);
            }
            _loc1_++;
         }
         this._SafeStr_16964 = [];
         if(!this._SafeStr_14561)
         {
            this.achievements = new Dictionary();
         }
         this._SafeStr_14561 = false;
      }
      
      private function _SafeStr_7801(param1:Array) : void
      {
         _SafeCls_112._SafeStr_3894 = int(param1[3]);
         var _loc2_:_SafeCls_79 = this.main._SafeStr_13608();
         _loc2_.updateInfoField(GuiConstants.USER_WINDOW,_SafeCls_130._SafeStr_13009,_SafeCls_125._SafeStr_14430);
         var _loc3_:_SafeCls_271 = _SafeCls_271.getInstance();
         _loc3_._SafeStr_7821();
         _loc2_._SafeStr_4589();
         _loc2_._SafeStr_9789();
      }
      
      private function _SafeStr_6944(param1:Array) : void
      {
         var _loc2_:String = String(param1[2]);
         var _loc3_:int = int(param1[3]);
         switch(_loc2_)
         {
            case _SafeCls_101._SafeStr_11268:
               _SafeCls_112._SafeStr_13001 = _loc3_;
               break;
            case _SafeCls_101._SafeStr_11566:
               _SafeCls_112._SafeStr_10461 = _loc3_;
               break;
            case _SafeCls_101._SafeStr_4450:
               _SafeCls_112._SafeStr_16817 = _loc3_;
               break;
            case _SafeCls_101._SafeStr_15961:
               _SafeCls_112._SafeStr_5210 = _loc3_;
         }
         var _loc4_:_SafeCls_79 = this.main._SafeStr_13608();
         _loc4_.updateInfoField(GuiConstants.USER_WINDOW,_SafeCls_130._SafeStr_5828,_SafeCls_125._SafeStr_6530);
      }
      
      private function _SafeStr_4104(param1:Array) : void
      {
         _SafeCls_112._SafeStr_14165 = Number(param1[3]);
         this.main._SafeStr_13608().updateInfoField(GuiConstants.USER_WINDOW,_SafeCls_130._SafeStr_5828,_SafeCls_125._SafeStr_8565);
         _SafeCls_112._SafeStr_14247 = parseFloat(param1[4]);
         this.main._SafeStr_13608().updateInfoField(GuiConstants.USER_WINDOW,_SafeCls_130._SafeStr_5828,_SafeCls_125._SafeStr_9698);
         var _loc2_:_SafeCls_271 = _SafeCls_271.getInstance();
         _loc2_._SafeStr_7593();
      }
      
      private function _SafeStr_9576(param1:Array) : void
      {
         _SafeCls_112._SafeStr_18240 = int(param1[3]);
         this.main._SafeStr_13608().updateInfoField(GuiConstants.USER_WINDOW,_SafeCls_130._SafeStr_13009,_SafeCls_125._SafeStr_9924);
      }
      
      private function _SafeStr_9349(param1:Array) : void
      {
      }
      
      private function _SafeStr_13975(param1:Array) : void
      {
         var _loc7_:int = 0;
         var _loc8_:_SafeCls_425 = null;
         var _loc2_:int = 2;
         _loc2_++;
         var _loc3_:int = int(param1[++_loc2_]);
         var _loc4_:String = param1[++_loc2_];
         var _loc5_:int = int(param1[++_loc2_]);
         var _loc6_:int = int(param1[++_loc2_]);
         if(map != null)
         {
            _loc8_ = map._SafeStr_16656(_loc3_,_SafeCls_425) as _SafeCls_425;
            if(_loc8_)
            {
               if(_loc4_ == _SafeCls_101._SafeStr_5453)
               {
                  _loc8_.shield.value = _loc5_;
                  _loc7_ = 3;
               }
               else if(_loc4_ == _SafeCls_101._SafeStr_3282)
               {
                  _loc8_.hp.value = _loc5_;
                  _loc7_ = 2;
               }
            }
            if(Settings.displayHitpointBubbles)
            {
               map.effects._SafeStr_14987(new _SafeCls_478(map._SafeStr_4053(_loc3_),_loc6_,_loc7_,true,false));
            }
         }
      }
      
      public function _SafeStr_5402(param1:int, param2:int, param3:int, param4:int) : void
      {
         if(this.hero != null)
         {
            if(Settings.JS_EVENT_TRACKING_ENABLED)
            {
               if(this.hero.hp.hp.value > param2 / 10 && param1 <= param2 / 10)
               {
                  _SafeCls_80.call(_SafeCls_80._SafeStr_13717,[_SafeCls_80._SafeStr_11221]);
               }
            }
            this.hero.hp.hp.value = param1;
            this.hero.hp._SafeStr_16978.value = param2;
            this.hero.hp._SafeStr_14309.value = param3;
            this.hero.hp._SafeStr_7136.value = param4;
         }
      }
      
      public function _SafeStr_7158(param1:int, param2:int) : void
      {
         var _loc3_:_SafeCls_120 = null;
         if(map != null)
         {
            _loc3_ = map.hero;
            if(_loc3_ != null)
            {
               _loc3_.hp.shield.value = param1;
               _loc3_.hp.maxShield.value = param2;
            }
         }
      }
      
      private function _SafeStr_8196(param1:Array) : void
      {
         var _loc2_:String = null;
         var _loc3_:String = null;
         var _loc4_:String = null;
         var _loc5_:int = 0;
         _loc2_ = param1[3];
         param1.splice(0,4);
         switch(param1.length)
         {
            case 0:
               this.main._SafeStr_13608().writeToLog(_SafeCls_122.getItem(_loc2_));
               break;
            case 1:
               _loc3_ = _SafeCls_122.getItem(param1[0]);
               if(_loc3_.length == 0)
               {
                  _loc3_ = param1[0];
               }
               this.main._SafeStr_13608().writeToLog(_SafeCls_122.getItem(_loc2_).replace("%!",_loc3_));
               break;
            default:
               _loc4_ = _SafeCls_122.getItem(_loc2_);
               _loc5_ = 0;
               while(_loc5_ < param1.length)
               {
                  _loc4_ = _loc4_.replace(param1[_loc5_],param1[_loc5_ + 1]);
                  _loc5_++;
                  _loc5_++;
               }
               this.main._SafeStr_13608().writeToLog(_loc4_);
         }
         if(_loc2_ == "jump_cpu_failed_attack" || _loc2_ == "jump_cpu_failed_attack2" || _loc2_ == "jump_cpu_failed_attack3" || _loc2_ == "jump_cpu_failed_ontarget" || _loc2_ == "jump_cpu_failed_map" || _loc2_ == "jump_cpu_malfunction" || _loc2_ == "jump_cpu_failed_time" || _loc2_ == "jump_cpu_failed_attack" || _loc2_ == "jumpgate_failed_pvp_map" || _loc2_ == "jumpgate_failed_no_gate")
         {
            _SafeCls_113.playSoundEffect(_SafeCls_176._SafeStr_17050);
         }
      }
      
      private function _SafeStr_7210(param1:Array) : void
      {
         var _loc2_:String = param1[3];
         this.main._SafeStr_13608().writeToLog(_loc2_);
      }
      
      private function get hero() : _SafeCls_120
      {
         return map ? map.hero : null;
      }
   }
}

Bellow are the obfuscated identifiers generated by the decompiler, you can ignore them, but they are provided here for reference in case you want to rename them to something more meaningful.
/** 
 * WARNING: The original code has obfuscated identifiers.
 * List of replacements follows:
 * @identifier _SafeCls_21 = "_-g2R"
 * @identifier _SafeCls_31 = "_-e41"
 * @identifier _SafeCls_67 = "_-J2P"
 * @identifier _SafeCls_79 = "_-Q3K"
 * @identifier _SafeCls_80 = "_-T4Z"
 * @identifier _SafeCls_83 = "_-F1p"
 * @identifier _SafeCls_88 = "_-636"
 * @identifier _SafeCls_101 = "_-t3Z"
 * @identifier _SafeCls_112 = "_-l3H"
 * @identifier _SafeCls_113 = "_-R4g"
 * @identifier _SafeCls_119 = "_-r1d"
 * @identifier _SafeCls_120 = "_-834"
 * @identifier _SafeCls_122 = "_-P43"
 * @identifier _SafeCls_125 = "_-73N"
 * @identifier _SafeCls_130 = "_-i3Z"
 * @identifier _SafeCls_135 = "_-b2Z"
 * @identifier _SafeCls_155 = "_-K4Q"
 * @identifier _SafeCls_160 = "_-X1c"
 * @identifier _SafeCls_170 = "_-E4C"
 * @identifier _SafeCls_176 = "_-p1j"
 * @identifier _SafeCls_181 = "_-k4Y"
 * @identifier _SafeCls_266 = "_-941"
 * @identifier _SafeCls_271 = "_-X1b"
 * @identifier _SafeCls_390 = "_-Tu"
 * @identifier _SafeCls_391 = "_-LZ"
 * @identifier _SafeCls_393 = "_-s1O"
 * @identifier _SafeCls_425 = "_-o1d"
 * @identifier _SafeCls_464 = "_-p2q"
 * @identifier _SafeCls_478 = "_-s2A"
 * @identifier _SafeCls_486 = "_-U4L"
 * @identifier _SafeCls_496 = "_-W15"
 * @identifier _SafeCls_498 = "_-G11"
 * @identifier _SafeCls_499 = "_-K4T"
 * @identifier _SafeCls_500 = "_-51D"
 * @identifier _SafeCls_501 = "_-g2A"
 * @identifier _SafeCls_502 = "_-84M"
 * @identifier _SafeCls_503 = "_-D4E"
 * @identifier _SafeCls_504 = "_-h2n"
 * @identifier _SafePkg_32 = "_-a2H"
 * @identifier _SafePkg_54 = "_-D2G"
 * @identifier _SafePkg_84 = "_-T4U"
 * @identifier _SafePkg_100 = "_-B1T"
 * @identifier _SafePkg_114 = "_-a4n"
 * @identifier _SafePkg_118 = "_-o1k"
 * @identifier _SafePkg_126 = "_-oQ"
 * @identifier _SafePkg_131 = "_-c2O"
 * @identifier _SafePkg_137 = "_-21n"
 * @identifier _SafePkg_175 = "_-L1y"
 * @identifier _SafePkg_182 = "_-S1M"
 * @identifier _SafePkg_267 = "_-z3M"
 * @identifier _SafePkg_281 = "_-Zu"
 * @identifier _SafePkg_392 = "_-Q3i"
 * @identifier _SafePkg_497 = "_-I3R"
 * @identifier _SafeStr_3282 = "_-r2l"
 * @identifier _SafeStr_3358 = "_-o2S"
 * @identifier _SafeStr_3379 = "_-l4A"
 * @identifier _SafeStr_3496 = "_-34i"
 * @identifier _SafeStr_3506 = "_-945"
 * @identifier _SafeStr_3507 = "_-H44"
 * @identifier _SafeStr_3518 = "_-Z2I"
 * @identifier _SafeStr_3564 = "_-d2o"
 * @identifier _SafeStr_3614 = "_-c1n"
 * @identifier _SafeStr_3711 = "_-s2V"
 * @identifier _SafeStr_3894 = "_-cJ"
 * @identifier _SafeStr_3927 = "_-E3A"
 * @identifier _SafeStr_3931 = "_-M4s"
 * @identifier _SafeStr_4001 = "_-o3b"
 * @identifier _SafeStr_4045 = "_-q2x"
 * @identifier _SafeStr_4053 = "_-vp"
 * @identifier _SafeStr_4070 = "_-VM"
 * @identifier _SafeStr_4076 = "_-G1X"
 * @identifier _SafeStr_4104 = "_-e1V"
 * @identifier _SafeStr_4212 = "_-W3"
 * @identifier _SafeStr_4307 = "_-L2c"
 * @identifier _SafeStr_4333 = "_-WC"
 * @identifier _SafeStr_4361 = "_-o23"
 * @identifier _SafeStr_4405 = "_-Q3M"
 * @identifier _SafeStr_4450 = "_-T17"
 * @identifier _SafeStr_4540 = "_-E4S"
 * @identifier _SafeStr_4589 = "_-G2I"
 * @identifier _SafeStr_4680 = "_-n2v"
 * @identifier _SafeStr_4820 = "_-Y4V"
 * @identifier _SafeStr_4896 = "_-U4Q"
 * @identifier _SafeStr_4897 = "_-q1"
 * @identifier _SafeStr_4981 = "_-r3e"
 * @identifier _SafeStr_4998 = "_-U4g"
 * @identifier _SafeStr_5070 = "_-l2b"
 * @identifier _SafeStr_5073 = "_-Rf"
 * @identifier _SafeStr_5083 = "_-n6"
 * @identifier _SafeStr_5140 = "_-53L"
 * @identifier _SafeStr_5151 = "_-O6"
 * @identifier _SafeStr_5210 = "_-63O"
 * @identifier _SafeStr_5233 = "_-I3q"
 * @identifier _SafeStr_5283 = "_-j1g"
 * @identifier _SafeStr_5402 = "_-C4X"
 * @identifier _SafeStr_5453 = "_-P4z"
 * @identifier _SafeStr_5828 = "_-L1z"
 * @identifier _SafeStr_5910 = "_-V2F"
 * @identifier _SafeStr_6039 = "_-k1C"
 * @identifier _SafeStr_6052 = "_-Il"
 * @identifier _SafeStr_6091 = "_-112"
 * @identifier _SafeStr_6411 = "_-74U"
 * @identifier _SafeStr_6530 = "_-vh"
 * @identifier _SafeStr_6541 = "_-k2b"
 * @identifier _SafeStr_6579 = "_-Fg"
 * @identifier _SafeStr_6708 = "_-62n"
 * @identifier _SafeStr_6792 = "_-S8"
 * @identifier _SafeStr_6944 = "_-23R"
 * @identifier _SafeStr_7052 = "_-J2T"
 * @identifier _SafeStr_7070 = "_-R1e"
 * @identifier _SafeStr_7126 = "_-G2q"
 * @identifier _SafeStr_7136 = "_-D2z"
 * @identifier _SafeStr_7158 = "_-E3w"
 * @identifier _SafeStr_7210 = "_-e8"
 * @identifier _SafeStr_7347 = "_-C1i"
 * @identifier _SafeStr_7394 = "_-62r"
 * @identifier _SafeStr_7398 = "_-PQ"
 * @identifier _SafeStr_7405 = "_-iT"
 * @identifier _SafeStr_7418 = "_-u4"
 * @identifier _SafeStr_7432 = "_-P2I"
 * @identifier _SafeStr_7483 = "_-C4n"
 * @identifier _SafeStr_7524 = "_-121"
 * @identifier _SafeStr_7561 = "_-92L"
 * @identifier _SafeStr_7593 = "_-b4E"
 * @identifier _SafeStr_7650 = "_-1M"
 * @identifier _SafeStr_7711 = "_-h22"
 * @identifier _SafeStr_7801 = "_-z22"
 * @identifier _SafeStr_7802 = "_-84Z"
 * @identifier _SafeStr_7821 = "_-gh"
 * @identifier _SafeStr_7822 = "_-V2t"
 * @identifier _SafeStr_7938 = "_-H36"
 * @identifier _SafeStr_8099 = "_-S3k"
 * @identifier _SafeStr_8196 = "_-nB"
 * @identifier _SafeStr_8259 = "_-G3n"
 * @identifier _SafeStr_8375 = "_-F0"
 * @identifier _SafeStr_8565 = "_-L41"
 * @identifier _SafeStr_8573 = "_-F1I"
 * @identifier _SafeStr_8618 = "_-215"
 * @identifier _SafeStr_8703 = "_-g1F"
 * @identifier _SafeStr_8834 = "_-e1D"
 * @identifier _SafeStr_8840 = "_-d1S"
 * @identifier _SafeStr_8961 = "_-k2K"
 * @identifier _SafeStr_9112 = "_-A3y"
 * @identifier _SafeStr_9260 = "_-D1x"
 * @identifier _SafeStr_9340 = "_-E4N"
 * @identifier _SafeStr_9349 = "_-q13"
 * @identifier _SafeStr_9427 = "_-K4J"
 * @identifier _SafeStr_9571 = "_-e1l"
 * @identifier _SafeStr_9576 = "_-u3V"
 * @identifier _SafeStr_9627 = "_-O4l"
 * @identifier _SafeStr_9639 = "_-91g"
 * @identifier _SafeStr_9698 = "_-J4o"
 * @identifier _SafeStr_9728 = "_-e2G"
 * @identifier _SafeStr_9789 = "_-14u"
 * @identifier _SafeStr_9910 = "_-43K"
 * @identifier _SafeStr_9917 = "_-Z2h"
 * @identifier _SafeStr_9920 = "_-X1z"
 * @identifier _SafeStr_9924 = "_-04x"
 * @identifier _SafeStr_10010 = "_-e37"
 * @identifier _SafeStr_10047 = "_-W1i"
 * @identifier _SafeStr_10090 = "_-g4x"
 * @identifier _SafeStr_10283 = "_-612"
 * @identifier _SafeStr_10377 = "_-o3m"
 * @identifier _SafeStr_10461 = "_-C5"
 * @identifier _SafeStr_10562 = "_-24X"
 * @identifier _SafeStr_10659 = "_-V17"
 * @identifier _SafeStr_10819 = "_-k4C"
 * @identifier _SafeStr_11020 = "_-x3l"
 * @identifier _SafeStr_11080 = "_-v3u"
 * @identifier _SafeStr_11221 = "_-Y4L"
 * @identifier _SafeStr_11268 = "_-R3J"
 * @identifier _SafeStr_11403 = "_-62l"
 * @identifier _SafeStr_11476 = "_-Uk"
 * @identifier _SafeStr_11566 = "_-PG"
 * @identifier _SafeStr_11589 = "_-8v"
 * @identifier _SafeStr_11611 = "_-L6"
 * @identifier _SafeStr_11842 = "_-E3p"
 * @identifier _SafeStr_12038 = "_-AZ"
 * @identifier _SafeStr_12063 = "_-S3z"
 * @identifier _SafeStr_12135 = "_-ux"
 * @identifier _SafeStr_12149 = "_-u7"
 * @identifier _SafeStr_12260 = "_-71h"
 * @identifier _SafeStr_12327 = "_-k3v"
 * @identifier _SafeStr_12339 = "_-Zz"
 * @identifier _SafeStr_12474 = "_-H3D"
 * @identifier _SafeStr_12479 = "_-p3r"
 * @identifier _SafeStr_12557 = "_-ab"
 * @identifier _SafeStr_12610 = "_-EO"
 * @identifier _SafeStr_12637 = "_-32i"
 * @identifier _SafeStr_13001 = "_-W1J"
 * @identifier _SafeStr_13009 = "_-sa"
 * @identifier _SafeStr_13075 = "_-3c"
 * @identifier _SafeStr_13080 = "_-a27"
 * @identifier _SafeStr_13139 = "_-l4L"
 * @identifier _SafeStr_13182 = "_-X3O"
 * @identifier _SafeStr_13291 = "_-p3y"
 * @identifier _SafeStr_13293 = "_-V4S"
 * @identifier _SafeStr_13369 = "_-9I"
 * @identifier _SafeStr_13535 = "_-w3U"
 * @identifier _SafeStr_13608 = "_-3g"
 * @identifier _SafeStr_13663 = "_-h29"
 * @identifier _SafeStr_13717 = "_-M4I"
 * @identifier _SafeStr_13733 = "_-sU"
 * @identifier _SafeStr_13763 = "_-6e"
 * @identifier _SafeStr_13794 = "_-q2O"
 * @identifier _SafeStr_13936 = "_-Y4a"
 * @identifier _SafeStr_13975 = "_-d1J"
 * @identifier _SafeStr_14001 = "_-s3J"
 * @identifier _SafeStr_14011 = "_-H47"
 * @identifier _SafeStr_14029 = "_-aL"
 * @identifier _SafeStr_14050 = "_-kd"
 * @identifier _SafeStr_14118 = "_-M2M"
 * @identifier _SafeStr_14165 = "_-I2H"
 * @identifier _SafeStr_14247 = "_-7B"
 * @identifier _SafeStr_14251 = "_-gb"
 * @identifier _SafeStr_14254 = "_-12s"
 * @identifier _SafeStr_14309 = "_-L4J"
 * @identifier _SafeStr_14430 = "_-Pw"
 * @identifier _SafeStr_14447 = "_-v38"
 * @identifier _SafeStr_14533 = "_-T4l"
 * @identifier _SafeStr_14561 = "_-01o"
 * @identifier _SafeStr_14637 = "_-ik"
 * @identifier _SafeStr_14813 = "_-X4V"
 * @identifier _SafeStr_14844 = "_-E4p"
 * @identifier _SafeStr_14980 = "_-9D"
 * @identifier _SafeStr_14985 = "_-p2V"
 * @identifier _SafeStr_14987 = "_-z1s"
 * @identifier _SafeStr_15100 = "_-d4w"
 * @identifier _SafeStr_15130 = "_-s2r"
 * @identifier _SafeStr_15219 = "_-HB"
 * @identifier _SafeStr_15226 = "_-3s"
 * @identifier _SafeStr_15304 = "_-Y41"
 * @identifier _SafeStr_15315 = "_-j43"
 * @identifier _SafeStr_15571 = "_-e22"
 * @identifier _SafeStr_15641 = "_-fz"
 * @identifier _SafeStr_15961 = "_-21V"
 * @identifier _SafeStr_15969 = "_-y3q"
 * @identifier _SafeStr_16113 = "_-c1Z"
 * @identifier _SafeStr_16139 = "_-Y4c"
 * @identifier _SafeStr_16179 = "_-U2c"
 * @identifier _SafeStr_16305 = "_-Y2v"
 * @identifier _SafeStr_16513 = "_-L3G"
 * @identifier _SafeStr_16656 = "_-Z3m"
 * @identifier _SafeStr_16687 = "_-H46"
 * @identifier _SafeStr_16817 = "_-L1K"
 * @identifier _SafeStr_16887 = "_-w2t"
 * @identifier _SafeStr_16902 = "_-j4S"
 * @identifier _SafeStr_16912 = "_-S2m"
 * @identifier _SafeStr_16964 = "_-S2E"
 * @identifier _SafeStr_16978 = "_-F4a"
 * @identifier _SafeStr_17050 = "_-tA"
 * @identifier _SafeStr_17061 = "_-f48"
 * @identifier _SafeStr_17328 = "_-h33"
 * @identifier _SafeStr_17416 = "_-29"
 * @identifier _SafeStr_17499 = "_-Y1f"
 * @identifier _SafeStr_17597 = "_-k3I"
 * @identifier _SafeStr_17768 = "_-M31"
 * @identifier _SafeStr_17955 = "_-O2V"
 * @identifier _SafeStr_17978 = "_-R2"
 * @identifier _SafeStr_17989 = "_-642"
 * @identifier _SafeStr_18061 = "_-r1V"
 * @identifier _SafeStr_18117 = "_-j4s"
 * @identifier _SafeStr_18136 = "_-19"
 * @identifier _SafeStr_18240 = "_-n2H"
 */

# 动态生成实际使用的css样式
```csharp
// html
<p class="mg-20"></p>
<div class="layout-main"></p>
// 生成输出css文件包含
.mg-20 {}
.layout-main{}
```
## 生成规则
1. 从已有的css文件查找出规则,根据类名".class"
```csharp
  // 源css文件
  .txt-red{color:red}
  .txt-blue{color:blue}
  // html
  <p class="txt-red"></p>
  // 匹配到txt-red,选取输出
  .txt-red
  {
    color:red
  }
```
2. 根据类名动态生成规则
```csharp
  <p class="mg-20"></p>
  // 根据简化样式名 mg-20 生成输出
  .mg-20 {
    margin:20px;
  }
```
## 使用
主要文件UsedCss.cs
```csharp
new UsedCss().Run("index.html", "output.css");
```
## 处理过程
1. 载入规则集,忽略样式列表,明确加入样式列表
2. 取出html中所有使用到的class,去掉忽略的,加入明确的
3. 遍历class,到规则集匹配,抽取/生成css样式.
4. 简化合并相同规则集
5. 输出css文件

## 默认数据
#### 在dataDefault目录下
1. simpleRules.ini 常用css简单规则,一般只包含一个规则的原子风格.
2. styleSimpleName.ini 类简化名字和样式规则对应关系
3. styleValues.ini 预定义样式值

#### ini文件格式

1. 用等号分割键和值 "k=v" ,可用 "// 注释",键/值前后不要空格.每个键值对或者注释占一行.行尾不能有分号.
2. 值部分是css规则,一般一条,多条用;(分号)隔开,最后面的规则不能有分号.
3. simpleRules的键是简化类名字,值是css规则例如:
```ini
  txt-center=text-align:center
```
4. styleSimpleName和simpleRules相似,键也是简化名字,值也是css规则,但规则的值是占位符"$v",具体值动态生成后替换.例如:
```ini
  // $v值示例: 20px / 20vw / 20rem / 20%
  mg=margin:$v
  bg-black,bg-white=background-color:$v
```
5. styleValues的键是类简化名字的后部分,值是css规则值.例如:
```ini
  // black0表示颜色值是 #000
  // black0对应简化类名 "bg-black-0" 的后2段
  black0=#000
```

## 特点
#### 简化类名组成规则
```
[媒体查询-]pre[-规则名字][-规则值名字]-val
```
其中pre为前缀,val为值.
```csharp
// 例子:
mg-20
// mg是前缀,表示规则名字margin. 20是值,表示20px. 转换规则是 margin:20px

mg-l-20
// l是规则名字,表示 margin-left. 结果 margin-left:20px

bg-red-5
// bg表示background规则,red表示颜色规则,5是颜色深度等级.
// 结果: background-color:#b91c1c
```
支持媒体查询前缀,有4个 sm md lg xl
```css
/*sm是明天查询前缀,bg-red-5是规则和值,写在媒体查询定义内*/ 

sm-bg-teal-5

/*结果*/
@media (max-width: 639.9px) {
  .sm-bg-teal-5 {
    background-color: #14b8a6;
  }
}
```

#### 复杂匹配
&emsp;没有实现这个,太复杂了.浏览器在解析html和css时,能将class和css里与之有关联的规则匹配到,然后渲染样式.根据类名到样式表中取出使用到的规则,也需要实现浏览器的这种功能.

&emsp;要分析出css文件里,那些规则对html起了作用,需要解决的问题至少有:简单的比如.btn,复杂的就多了,.btn.active.伪类,.btn:after,子元素.btn > span.还有媒体查询,css动画等.这比较复杂了,全部实现好难啊.

&emsp;也可以用专业的库postcss解决.现在的解决办法是尽量使用原子样式,复合样式拆解成单个文件,按需加入.只要使用到了里面的一个规则,也全部加入.最后打包输出一个css文件.
## 工具
[AngleSharp](https://anglesharp.github.io/) 用于分析html和css
## 其它目录和文件
1. outcss 含有最终输出的css文件,为各种项目生成的css.
2. fullTools 包含了所有工具样式的文件,为测试工具样式效果时用.
3. StylesGlobal.cs 返回全局一致性css.Styles开头的类是源css内容,提供选取.可以输出到最终css.
4. ignoreClass 要忽略的样式类的配置文件目录